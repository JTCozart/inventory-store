#!/usr/bin/env bash
#
# Installs the newest GitHub release of one app on this server, with a health
# check and automatic rollback. Shared by every Cozart web app; the app is
# described by an app.conf file (see app.conf.sample).
#
# Run by the <slug>-autoupdate.timer (installed by setup-autodeploy.sh). By hand:
#   sudo APP_CONF=/etc/<slug>/app.conf <slug>-autoupdate           # install if newer
#   sudo APP_CONF=/etc/<slug>/app.conf <slug>-autoupdate --check   # only report
#
# Channel (in /etc/<slug>/autoupdate.env):
#   CHANNEL=stable       installs releases/latest, which GitHub never points at a prerelease
#   CHANNEL=prerelease   installs the newest published release of any kind
set -euo pipefail

APP_CONF="${APP_CONF:?APP_CONF must point at the app.conf for this app}"
[ -f "$APP_CONF" ] || { echo "ERROR: $APP_CONF not found." >&2; exit 1; }
# shellcheck disable=SC1090
source "$APP_CONF"

: "${APP_SLUG:?APP_SLUG not set in $APP_CONF}"
: "${SERVICE_USER:?SERVICE_USER not set in $APP_CONF}"
: "${BINARY:?BINARY not set in $APP_CONF}"
SERVICE_NAME="${SERVICE_NAME:-$APP_SLUG}"
INSTALL_DIR="${INSTALL_DIR:-/opt/$APP_SLUG}"
BACKUP_DIR="${BACKUP_DIR:-/opt/$APP_SLUG-backups}"
BACKUP_KEEP="${BACKUP_KEEP:-5}"
DATA_PATHS="${DATA_PATHS:-}"
CODE_OWNER="${CODE_OWNER:-service}"
DEFAULT_PORT="${DEFAULT_PORT:-}"
PORT_ENV_NAMES="${PORT_ENV_NAMES:-ASPNETCORE_URLS Proxy__Port PORT}"
HEALTH_PATH="${HEALTH_PATH:-/health}"
HEALTH_RETRIES="${HEALTH_RETRIES:-15}"
HEALTH_INTERVAL="${HEALTH_INTERVAL:-2}"
LOCK_FILE="${LOCK_FILE:-/run/lock/cozart-deploy.lock}"
LOCK_WAIT_SECONDS="${LOCK_WAIT_SECONDS:-900}"
ENV_FILE="${ENV_FILE:-/etc/$APP_SLUG/autoupdate.env}"
WORKER_PATH="${WORKER_PATH:-/usr/local/bin/$APP_SLUG-autoupdate}"
GITHUB_API_URL="${GITHUB_API_URL:-https://api.github.com}"
STATE_DIR="${STATE_DIR:-/var/lib/$APP_SLUG-autoupdate}"
FAILED_RELEASE_FILE="$STATE_DIR/failed-release"

# Under systemd, stdout already reaches the journal, so logging twice would repeat every line.
log() {
    [ -n "${JOURNAL_STREAM:-}" ] || logger -t "$APP_SLUG-autoupdate" -- "$*" 2>/dev/null || true
    printf '%s\n' "$*"
}
err() { log "ERROR: $*"; exit 1; }

[ "$(id -u)" -eq 0 ] || err "Must run as root."
[ -f "$ENV_FILE" ] || err "$ENV_FILE not found. Run setup-autodeploy.sh first."

# shellcheck disable=SC1090
source "$ENV_FILE"
: "${GITHUB_REPO:?GITHUB_REPO not set in $ENV_FILE}"
: "${GITHUB_TOKEN:?GITHUB_TOKEN not set in $ENV_FILE}"
CHANNEL="${CHANNEL:-stable}"
ASSET_PATTERN="${ASSET_PATTERN:-linux-x64.tar.gz}"
case "$CHANNEL" in
    stable|prerelease) ;;
    *) err "CHANNEL must be 'stable' or 'prerelease' in $ENV_FILE (got '$CHANNEL')." ;;
esac

command -v curl >/dev/null 2>&1 || err "curl is required."
command -v jq >/dev/null 2>&1 || err "jq is required (apt-get install -y jq)."

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# --- GitHub ---------------------------------------------------------------------

HTTP_STATUS=""
github_get() {
    local url="$1" output_file="$2"
    HTTP_STATUS="$(curl -sS -o "$output_file" -w '%{http_code}' \
        -H "Authorization: Bearer $GITHUB_TOKEN" \
        -H "Accept: application/vnd.github+json" \
        -H "X-GitHub-Api-Version: 2022-11-28" \
        "$url" 2>/dev/null)" || HTTP_STATUS="000"
}

fail_on_bad_status() {
    case "$HTTP_STATUS" in
        200) ;;
        401|403) err "GitHub rejected the token (HTTP $HTTP_STATUS). It has probably expired: create a new read-only token and update GITHUB_TOKEN in $ENV_FILE." ;;
        404) err "GitHub returned 404 for $GITHUB_REPO: no release published yet, or the token cannot see the repository." ;;
        *) err "GitHub request failed (HTTP $HTTP_STATUS) for $GITHUB_REPO." ;;
    esac
}

select_release() {
    local response_file="$WORK_DIR/release-response.json"
    if [ "$CHANNEL" = "stable" ]; then
        github_get "$GITHUB_API_URL/repos/$GITHUB_REPO/releases/latest" "$response_file"
        fail_on_bad_status
        RELEASE_JSON="$(cat "$response_file")"
    else
        github_get "$GITHUB_API_URL/repos/$GITHUB_REPO/releases?per_page=10" "$response_file"
        fail_on_bad_status
        RELEASE_JSON="$(jq -c '[.[] | select(.draft == false)][0] // empty' "$response_file")"
    fi
    LATEST_TAG="$(printf '%s' "$RELEASE_JSON" | jq -r '.tag_name // empty')"
    [ -n "$LATEST_TAG" ] || err "No published release found for $GITHUB_REPO on channel '$CHANNEL'."
}

CURRENT_VERSION="$(cat "$INSTALL_DIR/VERSION" 2>/dev/null || echo "unknown")"
select_release

# A release that failed its health check stays the newest release until someone publishes another,
# so without this the timer would reinstall it, and take the app down for a minute, every 10 minutes.
if [ "$LATEST_TAG" != "$CURRENT_VERSION" ] && [ "$(cat "$FAILED_RELEASE_FILE" 2>/dev/null | head -1)" = "$LATEST_TAG" ]; then
    log "Skipping $LATEST_TAG: it already failed its health check on this server. Publish a newer release, or delete $FAILED_RELEASE_FILE to retry it."
    exit 0
fi

if [ "${1:-}" = "--check" ]; then
    if [ "$LATEST_TAG" = "$CURRENT_VERSION" ]; then
        log "Up to date on channel $CHANNEL (current=$CURRENT_VERSION)."
    else
        log "Update available on channel $CHANNEL: $CURRENT_VERSION -> $LATEST_TAG"
    fi
    exit 0
fi

if [ "$LATEST_TAG" = "$CURRENT_VERSION" ]; then
    log "Up to date on channel $CHANNEL (current=$CURRENT_VERSION). Nothing to do."
    exit 0
fi

# --- One update at a time per host ------------------------------------------------
# Restarting two apps together would hide which one broke, so every app on the host
# shares this lock and waits its turn.
mkdir -p "$(dirname "$LOCK_FILE")"
exec 9>"$LOCK_FILE"
flock -w "$LOCK_WAIT_SECONDS" 9 || err "Another update held $LOCK_FILE for $LOCK_WAIT_SECONDS seconds; will retry on the next timer run."

# Another app's update may have taken time, so the release list could have moved on.
CURRENT_VERSION="$(cat "$INSTALL_DIR/VERSION" 2>/dev/null || echo "unknown")"
select_release
if [ "$LATEST_TAG" = "$CURRENT_VERSION" ]; then
    log "Already at $CURRENT_VERSION after waiting for the lock."
    exit 0
fi
log "Update available on channel $CHANNEL: $CURRENT_VERSION -> $LATEST_TAG"

# --- Port for the health check --------------------------------------------------------

resolve_port() {
    local environment name value port
    environment="$(systemctl show "$SERVICE_NAME" -p Environment 2>/dev/null | sed 's/^Environment=//' || true)"
    for name in $PORT_ENV_NAMES; do
        value="$(printf '%s' "$environment" | tr ' ' '\n' | sed -n "s/^${name}=//p" | tail -1)"
        [ -n "$value" ] || continue
        port="$(printf '%s' "$value" | grep -oE '[0-9]+' | tail -1 || true)"
        if [ -n "$port" ]; then printf '%s' "$port"; return; fi
    done
    printf '%s' "$DEFAULT_PORT"
}
PORT="$(resolve_port)"
[ -n "$PORT" ] || err "Could not work out the port for $SERVICE_NAME: set DEFAULT_PORT in $APP_CONF."

is_healthy() { curl -sf "http://127.0.0.1:$PORT$HEALTH_PATH" >/dev/null 2>&1; }

wait_until_healthy() {
    for _ in $(seq 1 "$HEALTH_RETRIES"); do
        if is_healthy; then return 0; fi
        sleep "$HEALTH_INTERVAL"
    done
    return 1
}

# --- Download and unpack ------------------------------------------------------------

ASSET_API_URL="$(printf '%s' "$RELEASE_JSON" | jq -r --arg pattern "$ASSET_PATTERN" \
    '[.assets[] | select(.name | contains($pattern))][0].url // empty')"
[ -n "$ASSET_API_URL" ] || err "No release asset matching '$ASSET_PATTERN' on $LATEST_TAG."

TARBALL="$WORK_DIR/release.tar.gz"
log "Downloading $LATEST_TAG..."
curl -sfL -H "Authorization: Bearer $GITHUB_TOKEN" -H "Accept: application/octet-stream" \
    -o "$TARBALL" "$ASSET_API_URL" || err "Download failed."

EXTRACT_DIR="$WORK_DIR/extracted"
mkdir -p "$EXTRACT_DIR"
tar xzf "$TARBALL" -C "$EXTRACT_DIR" || err "Could not extract the release tarball."

# The bundle has one top-level folder named after the app.
NEW_APP_DIR="$(find "$EXTRACT_DIR" -mindepth 1 -maxdepth 1 -type d | head -1)"
[ -n "$NEW_APP_DIR" ] && [ -f "$NEW_APP_DIR/$BINARY" ] || err "The release does not look like a $APP_SLUG bundle (missing $BINARY)."

# --- Data paths: relative ones live in the install dir, absolute ones elsewhere ------------

absolute_data_paths() {
    local entry
    for entry in $DATA_PATHS; do
        case "$entry" in /*) ;; *) entry="$INSTALL_DIR/$entry" ;; esac
        compgen -G "$entry" || true
    done
}

# --- Stop, back up while nothing is writing, then swap -----------------------------------

BACKUP_STAMP="$(date +%Y%m%d-%H%M%S)-${CURRENT_VERSION}"
BACKUP_FILE="$BACKUP_DIR/$APP_SLUG-preupdate-$BACKUP_STAMP.tar.gz"
EXTERNAL_DATA_BACKUP_FILE="$BACKUP_DIR/$APP_SLUG-preupdate-$BACKUP_STAMP-external-data.tar.gz"
HAS_EXTERNAL_DATA=false

backup_current_install() {
    local external_paths=()
    mkdir -p "$BACKUP_DIR"
    log "Backing up $INSTALL_DIR to $BACKUP_FILE..."
    tar czf "$BACKUP_FILE" -C "$(dirname "$INSTALL_DIR")" "$(basename "$INSTALL_DIR")" \
        || err "Backup failed; the running install was left stopped but untouched."
    tar tzf "$BACKUP_FILE" >/dev/null 2>&1 || err "Backup archive is unreadable; aborting."

    while IFS= read -r path; do
        case "$path" in "$INSTALL_DIR"/*) ;; *) external_paths+=("${path#/}") ;; esac
    done < <(absolute_data_paths)

    if [ "${#external_paths[@]}" -gt 0 ]; then
        tar czf "$EXTERNAL_DATA_BACKUP_FILE" -C / "${external_paths[@]}" \
            || err "Data backup failed; aborting."
        tar tzf "$EXTERNAL_DATA_BACKUP_FILE" >/dev/null 2>&1 || err "Data backup archive is unreadable; aborting."
        HAS_EXTERNAL_DATA=true
    fi
}

apply_ownership() {
    local path
    if [ "$CODE_OWNER" = "root" ]; then
        chown -R root:root "$INSTALL_DIR"
        while IFS= read -r path; do chown -R "$SERVICE_USER":"$SERVICE_USER" "$path"; done < <(absolute_data_paths)
    else
        chown -R "$SERVICE_USER":"$SERVICE_USER" "$INSTALL_DIR"
    fi
    chmod -R go-w "$INSTALL_DIR"
}

record_failed_release() {
    mkdir -p "$STATE_DIR"
    printf '%s\n' "$LATEST_TAG" > "$FAILED_RELEASE_FILE"
}

restore_backup() {
    record_failed_release
    log "Restoring the pre-update backup..."
    systemctl stop "$SERVICE_NAME" 2>/dev/null || true
    find "$INSTALL_DIR" -mindepth 1 -delete
    tar xzf "$BACKUP_FILE" -C "$(dirname "$INSTALL_DIR")"
    if [ "$HAS_EXTERNAL_DATA" = true ]; then
        tar xzf "$EXTERNAL_DATA_BACKUP_FILE" -C /
    fi
    apply_ownership
    systemctl start "$SERVICE_NAME" 2>/dev/null || true
}

copy_new_release_into_place() {
    local entry exclude_arguments=()
    for entry in $DATA_PATHS; do
        case "$entry" in /*) ;; *) exclude_arguments+=(--exclude "/$entry") ;; esac
    done
    if command -v rsync >/dev/null 2>&1; then
        # --checksum: a rebuilt file with the same size and timestamp must still be replaced.
        rsync -a --checksum --delete "${exclude_arguments[@]}" "$NEW_APP_DIR"/ "$INSTALL_DIR"/
    else
        local keep_arguments=() name
        for entry in $DATA_PATHS; do
            case "$entry" in /*) ;; *) keep_arguments+=(! -name "$entry") ;; esac
        done
        find "$INSTALL_DIR" -mindepth 1 -maxdepth 1 "${keep_arguments[@]}" -exec rm -rf {} +
        for name in $(cd "$NEW_APP_DIR" && ls -A); do
            case " $DATA_PATHS " in *" $name "*) continue ;; esac
            cp -r "$NEW_APP_DIR/$name" "$INSTALL_DIR"/
        done
    fi
    chmod +x "$INSTALL_DIR/$BINARY"
    apply_ownership
}

log "Stopping $SERVICE_NAME..."
systemctl stop "$SERVICE_NAME" 2>/dev/null || true
backup_current_install

log "Installing $LATEST_TAG..."
copy_new_release_into_place

log "Starting $SERVICE_NAME..."
if ! systemctl start "$SERVICE_NAME"; then
    log "systemctl start failed."
    restore_backup
    err "$LATEST_TAG failed to start; rolled back to $CURRENT_VERSION."
fi

log "Waiting for $HEALTH_PATH on port $PORT..."
if ! wait_until_healthy; then
    log "Health check failed after installing $LATEST_TAG."
    restore_backup
    if wait_until_healthy; then
        err "Rolled back to $CURRENT_VERSION after $LATEST_TAG failed its health check."
    fi
    err "Rollback to $CURRENT_VERSION ALSO failed its health check. $SERVICE_NAME may be down. Backup kept at $BACKUP_FILE."
fi

log "$LATEST_TAG is installed and healthy."
rm -f "$FAILED_RELEASE_FILE"

# The tag, not whatever the bundle carried, is the identity of what is installed.
# Otherwise the next run would never match and would reinstall the same release forever.
printf '%s' "$LATEST_TAG" > "$INSTALL_DIR/VERSION"
[ "$CODE_OWNER" = "root" ] || chown "$SERVICE_USER":"$SERVICE_USER" "$INSTALL_DIR/VERSION"

prune_old_backups() {
    local old_file
    # shellcheck disable=SC2010,SC2012
    { ls -1t "$BACKUP_DIR/$APP_SLUG"-preupdate-*.tar.gz 2>/dev/null \
        | grep -v -- '-external-data\.tar\.gz$' \
        | tail -n +$((BACKUP_KEEP + 1)) \
        | while read -r old_file; do
            rm -f "$old_file" "${old_file%.tar.gz}-external-data.tar.gz"
        done; } || true
}
prune_old_backups

# Pick up newer scripts and config shipped in this bundle, so a kit fix reaches the server
# with the next release. mv keeps the replacement atomic: bash is still reading this file.
refresh_from_bundle() {
    local source_file="$1" target_file="$2"
    [ -f "$source_file" ] || return 0
    cmp -s "$source_file" "$target_file" && return 0
    cp "$source_file" "$target_file.new" && chmod --reference="$target_file" "$target_file.new" 2>/dev/null
    mv -f "$target_file.new" "$target_file"
    log "Refreshed $target_file from the release bundle."
}
refresh_from_bundle "$NEW_APP_DIR/autoupdate.sh" "$WORKER_PATH"
refresh_from_bundle "$NEW_APP_DIR/app.conf" "$APP_CONF"

log "Update complete: $CURRENT_VERSION -> $LATEST_TAG (channel $CHANNEL)."
