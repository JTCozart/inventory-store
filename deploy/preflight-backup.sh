#!/usr/bin/env bash
#
# Takes a verified backup of an app's data before anything else touches the server.
#
#   sudo ./preflight-backup.sh                 take a backup now, verify it, write VERIFIED only if it passes
#   sudo ./preflight-backup.sh --check-recent  exit 0 only if a VERIFIED backup is under MAX_AGE_MINUTES old
#
# SQLite files (*.db, *.sqlite, *.sqlite3) are copied with SQLite's own online backup, which gives a consistent
# copy of a database in use, including rows still in its -wal file. The copy is opened and put through
# PRAGMA integrity_check. Everything else under the data paths goes into one tar archive that is listed back.
# The live files are only ever read. install.sh refuses to run on an app with data unless --check-recent passes.
#
# Settings come from app.conf (APP_CONF, or app.conf next to this script) or from the environment, so the first
# backup can run before the kit is on the server:
#   ssh root@host 'APP_SLUG=x INSTALL_DIR=/opt/x DATA_PATHS="Data /var/lib/x" bash -s' < preflight-backup.sh
#
# Needs python3 (with the sqlite3 module) for the database copy. Backups go to BACKUP_ROOT (default /root/backups).
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR=""
[ -n "${BASH_SOURCE[0]:-}" ] && SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONF="${APP_CONF:-${SCRIPT_DIR:+$SCRIPT_DIR/app.conf}}"
if [ -n "$CONF" ] && [ -f "$CONF" ]; then
    # shellcheck disable=SC1090
    source "$CONF"
fi

: "${APP_SLUG:?APP_SLUG is not set}"
INSTALL_DIR="${INSTALL_DIR:-/opt/$APP_SLUG}"
DATA_PATHS="${DATA_PATHS:-}"
BACKUP_ROOT="${BACKUP_ROOT:-/root/backups}"
MAX_AGE_MINUTES="${MAX_AGE_MINUTES:-120}"

err() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

[ -n "$DATA_PATHS" ] || { echo "$APP_SLUG has no data paths: nothing to back up."; exit 0; }

absolute_data_paths() {
    local entry
    for entry in $DATA_PATHS; do
        case "$entry" in /*) ;; *) entry="$INSTALL_DIR/$entry" ;; esac
        compgen -G "$entry" || true
    done
}

mapfile -t data_paths < <(absolute_data_paths)

if [ "${1:-}" = "--check-recent" ]; then
    if [ "${#data_paths[@]}" -eq 0 ]; then
        echo "No data exists yet for $APP_SLUG: nothing to protect."
        exit 0
    fi
    recent="$(find "$BACKUP_ROOT" -maxdepth 2 -path "$BACKUP_ROOT/$APP_SLUG-preflight-*/VERIFIED" -mmin "-$MAX_AGE_MINUTES" 2>/dev/null | head -1)"
    if [ -n "$recent" ]; then
        echo "Verified backup found: $(dirname "$recent")"
        exit 0
    fi
    err "No verified backup of $APP_SLUG from the last $MAX_AGE_MINUTES minutes. Run preflight-backup.sh first, then retry."
fi

[ "$(id -u)" -eq 0 ] || err "Please run as root."
command -v python3 >/dev/null 2>&1 || err "python3 is required for the database copy."
python3 -c 'import sqlite3' 2>/dev/null || err "python3 has no sqlite3 module."

[ "${#data_paths[@]}" -gt 0 ] || { echo "None of the data paths for $APP_SLUG exist yet: nothing to back up."; exit 0; }

BACKUP_DIR="$BACKUP_ROOT/$APP_SLUG-preflight-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_ROOT"
mkdir -m 700 "$BACKUP_DIR"

mapfile -t database_files < <(find "${data_paths[@]}" -type f \( -name '*.db' -o -name '*.sqlite' -o -name '*.sqlite3' \) 2>/dev/null | sort -u)

echo "Backing up $APP_SLUG to $BACKUP_DIR"

if [ "${#database_files[@]}" -gt 0 ]; then
    python3 - "$BACKUP_DIR" "${database_files[@]}" <<'PY'
import os, sqlite3, sys

backup_dir, sources = sys.argv[1], sys.argv[2:]
manifest = open(os.path.join(backup_dir, 'MANIFEST.txt'), 'a')
failed = False


def table_counts(connection):
    names = [row[0] for row in connection.execute(
        "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")]
    return {name: connection.execute('SELECT COUNT(*) FROM "{}"'.format(name.replace('"', '""'))).fetchone()[0]
            for name in names}


for source in sources:
    target = os.path.join(backup_dir, 'db', source.lstrip('/'))
    os.makedirs(os.path.dirname(target), exist_ok=True)
    try:
        live = sqlite3.connect('file:{}?mode=ro'.format(source), uri=True, timeout=30)
        copy = sqlite3.connect(target)
        live.backup(copy)
        copy.close()
        live.close()
        check = sqlite3.connect(target)
        verdict = check.execute('PRAGMA integrity_check').fetchone()[0]
        counts = table_counts(check) if verdict == 'ok' else {}
        check.close()
    except sqlite3.Error as error:
        verdict, counts = 'sqlite error: {}'.format(error), {}
    if verdict != 'ok':
        failed = True
        print('ERROR: {}: integrity check failed ({})'.format(source, verdict), file=sys.stderr)
        manifest.write('database {} FAILED integrity_check: {}\n'.format(source, verdict))
        continue
    manifest.write('database {} integrity_check ok, {} bytes\n'.format(source, os.path.getsize(target)))
    for name, count in counts.items():
        manifest.write('  table {} rows {}\n'.format(name, count))
    print('database {}: ok, {} tables, {} rows'.format(source, len(counts), sum(counts.values())))

manifest.close()
sys.exit(1 if failed else 0)
PY
fi

tar_arguments=(--exclude='*.db' --exclude='*.db-wal' --exclude='*.db-shm' --exclude='*.sqlite' --exclude='*.sqlite3'
               --exclude='*.sqlite-wal' --exclude='*.sqlite-shm')
relative_paths=()
for path in "${data_paths[@]}"; do relative_paths+=("${path#/}"); done
tar czf "$BACKUP_DIR/files.tar.gz" "${tar_arguments[@]}" -C / "${relative_paths[@]}" \
    || err "Could not archive the data files."
tar tzf "$BACKUP_DIR/files.tar.gz" >/dev/null 2>&1 || err "The archive of data files is unreadable."
{ echo "files archive: $(tar tzf "$BACKUP_DIR/files.tar.gz" | grep -vc '/$') files"; } >> "$BACKUP_DIR/MANIFEST.txt"

(cd "$BACKUP_DIR" && find . -type f ! -name SHA256SUMS ! -name VERIFIED -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)

date -u +%Y-%m-%dT%H:%M:%SZ > "$BACKUP_DIR/VERIFIED"
echo "Verified. $(du -sh "$BACKUP_DIR" | cut -f1) written to $BACKUP_DIR"
echo "Restore notes are in $BACKUP_DIR/MANIFEST.txt; databases are under $BACKUP_DIR/db/, other files in files.tar.gz."
