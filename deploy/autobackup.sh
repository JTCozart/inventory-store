#!/usr/bin/env bash
#
# Nightly data-only backup for one app: the paths named in DATA_PATHS (database, Data
# Protection keys, uploads), nothing that a release can recreate. Run by
# <slug>-backup.timer (installed by setup-autobackup.sh). By hand:
#   sudo APP_CONF=/etc/<slug>/app.conf ./autobackup.sh
#
# The copy is of live files. For SQLite in WAL mode, the -wal and -shm files are taken in the
# same archive, which is normally recoverable but not guaranteed consistent. The pre-update
# backup in autoupdate.sh is taken with the service stopped and is the consistent one.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

APP_CONF="${APP_CONF:?APP_CONF must point at the app.conf for this app}"
# shellcheck disable=SC1090
source "$APP_CONF"
: "${APP_SLUG:?}"
INSTALL_DIR="${INSTALL_DIR:-/opt/$APP_SLUG}"
DATA_PATHS="${DATA_PATHS:-}"
BACKUP_KEEP_NIGHTLY="${BACKUP_KEEP_NIGHTLY:-3}"

[ "$EUID" -eq 0 ] || { echo "Please run as root." >&2; exit 1; }
[ -n "$DATA_PATHS" ] || { echo "$APP_SLUG has no DATA_PATHS; nothing to back up."; exit 0; }

if [ -n "${BACKUP_HOME:-}" ]; then
    HOME_DIR="$BACKUP_HOME"
    OWNER="$(stat -c '%U' "$BACKUP_HOME" 2>/dev/null || id -un)"
else
    HOME_DIR="${HOME:-/root}"
    OWNER="$(id -un)"
fi
NIGHTLY_DIR="$HOME_DIR/backups/autobackups"
mkdir -p "$NIGHTLY_DIR"

backup_paths=()
# A path that does not exist yet (Data Protection keys appear on first use) is simply skipped.
for entry in $DATA_PATHS; do
    case "$entry" in /*) ;; *) entry="$INSTALL_DIR/$entry" ;; esac
    while IFS= read -r match; do
        backup_paths+=("${match#/}")
    done < <(compgen -G "$entry" || true)
done
[ "${#backup_paths[@]}" -gt 0 ] || { echo "None of the data paths for $APP_SLUG exist yet." >&2; exit 1; }

BACKUP_FILE="$NIGHTLY_DIR/$APP_SLUG-autobackup-$(date +%Y%m%d-%H%M%S).tar.gz"
if ! tar czf "$BACKUP_FILE" -C / "${backup_paths[@]}" || ! tar tzf "$BACKUP_FILE" >/dev/null 2>&1; then
    echo "Nightly backup of $APP_SLUG failed or is unreadable." >&2
    rm -f "$BACKUP_FILE"
    exit 1
fi

# Keep the newest few, and only delete after a good fresh one exists.
# shellcheck disable=SC2012
ls -1t "$NIGHTLY_DIR/$APP_SLUG"-autobackup-*.tar.gz | tail -n +$((BACKUP_KEEP_NIGHTLY + 1)) | while read -r old_file; do
    rm -f "$old_file"
done

chown -R "$OWNER" "$HOME_DIR/backups" 2>/dev/null || true
echo "Nightly backup created: $BACKUP_FILE"
