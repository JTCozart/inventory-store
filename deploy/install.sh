#!/usr/bin/env bash
#
# Installs (or reinstalls) one app from an extracted release bundle as a systemd service.
# The bundle holds the published app, this script, app.conf, the unit file and the other kit
# scripts. Safe to re-run: data paths are never touched, and the existing unit drop-ins stay.
#
#   sudo ./install.sh
#   sudo PORT=5243 ./install.sh      # replaces @PORT@ in the unit file
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/app.conf"
: "${APP_SLUG:?}"
: "${SERVICE_USER:?}"
: "${BINARY:?}"
SERVICE_NAME="${SERVICE_NAME:-$APP_SLUG}"
INSTALL_DIR="${INSTALL_DIR:-/opt/$APP_SLUG}"
BACKUP_DIR="${BACKUP_DIR:-/opt/$APP_SLUG-backups}"
DATA_PATHS="${DATA_PATHS:-}"
CODE_OWNER="${CODE_OWNER:-service}"
PORT="${PORT:-${DEFAULT_PORT:-}}"
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mError:\033[0m %s\n' "$*" >&2; exit 1; }

[ "$(id -u)" -eq 0 ] || err "Please run as root (sudo ./install.sh)."
[ -f "$SCRIPT_DIR/$BINARY" ] || err "$BINARY is not next to this script. Run it from an extracted release bundle."
[ -f "$SCRIPT_DIR/$SERVICE_NAME.service" ] || err "$SERVICE_NAME.service is missing from the bundle."

# Git and Windows-built tarballs lose the execute bit.
chmod +x "$SCRIPT_DIR"/*.sh 2>/dev/null || true

# Real data is never touched without a verified backup taken first. Set SKIP_PREFLIGHT_CHECK=1 only for a
# throwaway server.
if [ "${SKIP_PREFLIGHT_CHECK:-0}" != "1" ] && [ -n "$DATA_PATHS" ]; then
    [ -f "$SCRIPT_DIR/preflight-backup.sh" ] || err "preflight-backup.sh is missing from the bundle."
    APP_CONF="$SCRIPT_DIR/app.conf" bash "$SCRIPT_DIR/preflight-backup.sh" --check-recent \
        || err "Nothing was changed. Run ./preflight-backup.sh and check it says Verified, then run this again."
fi

if ! id "$SERVICE_USER" >/dev/null 2>&1; then
    log "Creating system user '$SERVICE_USER'..."
    useradd --system --home "$INSTALL_DIR" --shell /usr/sbin/nologin "$SERVICE_USER"
fi

log "Stopping $SERVICE_NAME..."
systemctl stop "$SERVICE_NAME" 2>/dev/null || true

if [ -d "$INSTALL_DIR" ] && [ -n "$(ls -A "$INSTALL_DIR" 2>/dev/null)" ]; then
    mkdir -p "$BACKUP_DIR"
    backup_file="$BACKUP_DIR/$APP_SLUG-preinstall-$(date +%Y%m%d-%H%M%S).tar.gz"
    log "Backing up the current install to $backup_file..."
    tar czf "$backup_file" -C "$(dirname "$INSTALL_DIR")" "$(basename "$INSTALL_DIR")"
fi

log "Installing to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
exclude_arguments=()
for entry in $DATA_PATHS; do
    case "$entry" in /*) ;; *) exclude_arguments+=(--exclude "/$entry") ;; esac
done
if command -v rsync >/dev/null 2>&1; then
    rsync -a --checksum --delete "${exclude_arguments[@]}" "$SCRIPT_DIR"/ "$INSTALL_DIR"/
else
    keep_arguments=()
    for entry in $DATA_PATHS; do
        case "$entry" in /*) ;; *) keep_arguments+=(! -name "$entry") ;; esac
    done
    find "$INSTALL_DIR" -mindepth 1 -maxdepth 1 "${keep_arguments[@]}" -exec rm -rf {} +
    cp -r "$SCRIPT_DIR"/. "$INSTALL_DIR"/
fi
chmod +x "$INSTALL_DIR/$BINARY"

if [ "$CODE_OWNER" = "root" ]; then
    chown -R root:root "$INSTALL_DIR"
    for entry in $DATA_PATHS; do
        case "$entry" in /*) ;; *) entry="$INSTALL_DIR/$entry" ;; esac
        while IFS= read -r path; do chown -R "$SERVICE_USER":"$SERVICE_USER" "$path"; done < <(compgen -G "$entry" || true)
    done
else
    chown -R "$SERVICE_USER":"$SERVICE_USER" "$INSTALL_DIR"
fi
chmod -R go-w "$INSTALL_DIR"

log "Installing the systemd unit..."
install -m 0644 "$SCRIPT_DIR/$SERVICE_NAME.service" "$SERVICE_FILE"
if grep -q '@PORT@' "$SERVICE_FILE"; then
    [ -n "$PORT" ] || err "The unit uses @PORT@ but no PORT or DEFAULT_PORT is set."
    sed -i "s/@PORT@/$PORT/g" "$SERVICE_FILE"
fi
systemctl daemon-reload
systemctl enable "$SERVICE_NAME" >/dev/null 2>&1 || true
systemctl restart "$SERVICE_NAME"

log "Service status:"
systemctl --no-pager --full status "$SERVICE_NAME" | head -n 8 || true

cat <<EOF

$APP_SLUG installed from $(cat "$INSTALL_DIR/VERSION" 2>/dev/null || echo "an unversioned bundle").
  Logs:      journalctl -u $SERVICE_NAME -f
  Rollback:  the pre-install archive is in $BACKUP_DIR

Next, once per server:
  sudo ./setup-autodeploy.sh     # install new releases by itself (choose the channel)
  sudo ./setup-autobackup.sh     # nightly data backup
EOF
