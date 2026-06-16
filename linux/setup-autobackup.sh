#!/usr/bin/env bash
#
# Installs and starts a systemd timer that takes a data-only backup of
# Inventory Store every night into ~/backups/autobackups, keeping only the
# most recent backup (the stale one is deleted each run).
#
# Usage (from the extracted release directory):
#   sudo ./setup-autobackup.sh
#
# Expects autobackup.sh next to this script.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER=/usr/local/bin/inventorystore-autobackup
SERVICE_UNIT=/etc/systemd/system/inventorystore-backup.service
TIMER_UNIT=/etc/systemd/system/inventorystore-backup.timer

if [[ $EUID -ne 0 ]]; then
  echo "Please run as root: sudo ./setup-autobackup.sh" >&2
  exit 1
fi

if [[ ! -f "$SCRIPT_DIR/autobackup.sh" ]]; then
  echo "Could not find autobackup.sh next to this script." >&2
  exit 1
fi

# Resolve "~" to the human user's home, even when invoked via sudo. This home
# is baked into the service so the nightly run (which runs as root) backs up to
# the right place.
if [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != "root" ]]; then
  HOME_DIR="$(getent passwd "$SUDO_USER" | cut -d: -f6)"
else
  HOME_DIR="${HOME:-/root}"
fi

# 1. Install the backup worker.
install -m 0755 "$SCRIPT_DIR/autobackup.sh" "$WORKER"

# 2. Write the oneshot service that runs the worker.
cat > "$SERVICE_UNIT" <<EOF
[Unit]
Description=Inventory Store nightly backup
After=network.target

[Service]
Type=oneshot
Environment=BACKUP_HOME=$HOME_DIR
ExecStart=$WORKER
EOF

# 3. Write the timer that fires nightly (catching up if the machine was off).
cat > "$TIMER_UNIT" <<'EOF'
[Unit]
Description=Run Inventory Store nightly backup

[Timer]
OnCalendar=*-*-* 02:00:00
Persistent=true

[Install]
WantedBy=timers.target
EOF

# 4. Enable + start the timer.
systemctl daemon-reload
systemctl enable --now inventorystore-backup.timer

echo
echo "Nightly backup service installed."
echo "Backups are written to: $HOME_DIR/backups/autobackups (only the latest is kept)."
echo "Runs every night at 02:00."
echo
echo "Useful commands:"
echo "  systemctl list-timers inventorystore-backup.timer   # next run time"
echo "  systemctl start inventorystore-backup.service       # back up right now"
echo "  journalctl -u inventorystore-backup.service         # backup logs"
