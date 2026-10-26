#!/usr/bin/env bash
#
# Installs the nightly data backup timer (02:00) for one app. Run from the extracted bundle.
#   sudo ./setup-autobackup.sh              install and start
#   sudo ./setup-autobackup.sh --disable    remove
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/app.conf"
: "${APP_SLUG:?}"

WORKER="/usr/local/bin/$APP_SLUG-autobackup"
CONFIG_DIR="/etc/$APP_SLUG"
SERVICE_UNIT="/etc/systemd/system/$APP_SLUG-backup.service"
TIMER_UNIT="/etc/systemd/system/$APP_SLUG-backup.timer"

[ "$EUID" -eq 0 ] || { echo "Please run as root." >&2; exit 1; }

if [ "${1:-}" = "--disable" ]; then
    systemctl disable --now "$APP_SLUG-backup.timer" 2>/dev/null || true
    rm -f "$SERVICE_UNIT" "$TIMER_UNIT" "$WORKER"
    systemctl daemon-reload
    echo "Nightly backup disabled for $APP_SLUG."
    exit 0
fi

# Backups land in the home of whoever ran this, so a sudo user finds them without hunting.
BACKUP_HOME_DIR="${BACKUP_HOME:-$(getent passwd "${SUDO_USER:-root}" | cut -d: -f6)}"
BACKUP_HOME_DIR="${BACKUP_HOME_DIR:-/root}"

mkdir -p "$CONFIG_DIR"
install -m 0644 "$SCRIPT_DIR/app.conf" "$CONFIG_DIR/app.conf"
install -m 0755 "$SCRIPT_DIR/autobackup.sh" "$WORKER"

cat > "$SERVICE_UNIT" <<EOF
[Unit]
Description=$APP_SLUG - nightly data backup

[Service]
Type=oneshot
Environment=APP_CONF=$CONFIG_DIR/app.conf
Environment=BACKUP_HOME=$BACKUP_HOME_DIR
ExecStart=$WORKER
EOF

cat > "$TIMER_UNIT" <<EOF
[Unit]
Description=Nightly $APP_SLUG data backup

[Timer]
OnCalendar=*-*-* 02:00:00
RandomizedDelaySec=300
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now "$APP_SLUG-backup.timer"
echo "Nightly backup on for $APP_SLUG, written to $BACKUP_HOME_DIR/backups/autobackups."
echo "  systemctl list-timers $APP_SLUG-backup.timer"
