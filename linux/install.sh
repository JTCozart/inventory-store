#!/usr/bin/env bash
#
# Installs Inventory Store as a systemd service on Ubuntu / Debian.
#
# Usage (from the extracted release directory):
#   sudo ./install.sh
#
# Expects the published application in an "app" subdirectory next to this script.

set -euo pipefail

APP_DIR=/opt/inventorystore
SERVICE_USER=inventorystore
UNIT=/etc/systemd/system/inventorystore.service
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ $EUID -ne 0 ]]; then
  echo "Please run as root: sudo ./install.sh" >&2
  exit 1
fi

if [[ ! -f "$SCRIPT_DIR/app/InventoryStore.App" ]]; then
  echo "Could not find app/InventoryStore.App next to this script." >&2
  echo "Extract the full release archive and run install.sh from inside it." >&2
  exit 1
fi

# 1. Dedicated, login-less service account.
if ! id "$SERVICE_USER" &>/dev/null; then
  useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
fi

# 2. Stop any running instance before replacing files.
systemctl stop inventorystore 2>/dev/null || true

# 3. Copy the published app into place.
mkdir -p "$APP_DIR"
cp -r "$SCRIPT_DIR/app/." "$APP_DIR/"
chmod +x "$APP_DIR/InventoryStore.App"

# 4. Install the systemd unit.
cp "$SCRIPT_DIR/inventorystore.service" "$UNIT"

# 5. Enable + start at boot.
systemctl daemon-reload
systemctl enable inventorystore
systemctl restart inventorystore

echo
echo "Inventory Store is installed and running."
echo "Open http://localhost:5050 (or http://<this-host-ip>:5050) to create your admin account."
echo
echo "Useful commands:"
echo "  systemctl status inventorystore     # check service state"
echo "  journalctl -u inventorystore -f     # follow logs"
