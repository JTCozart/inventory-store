#!/usr/bin/env bash
#
# Updates an existing Inventory Store install to the latest GitHub release.
#
# What it does:
#   1. Takes a fresh data-only backup into ~/backups (deleting any previous
#      update backup first). If the backup fails, nothing else happens.
#   2. Downloads the latest linux-x64 release tarball into ~ and extracts it.
#   3. Runs the extracted install.sh (which stops the service, replaces the
#      app in /opt, and restarts the service).
#   4. Once the service is running again, deletes the tarball and any older
#      extracted version directories.
#
# Usage:
#   sudo ./update.sh
#
# Re-exec under bash if started with a POSIX shell.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi

set -euo pipefail

REPO="JTCozart/inventory-store"
DATA_DIR=/var/lib/inventorystore
SERVICE=inventorystore

if [[ $EUID -ne 0 ]]; then
  echo "Please run as root: sudo ./update.sh" >&2
  exit 1
fi

# Resolve "~" to the human user's home, even when invoked via sudo.
if [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != "root" ]]; then
  HOME_DIR="$(getent passwd "$SUDO_USER" | cut -d: -f6)"
  OWNER="$SUDO_USER"
else
  HOME_DIR="${HOME:-/root}"
  OWNER="$(id -un)"
fi

BACKUP_DIR="$HOME_DIR/backups"

# ---------------------------------------------------------------------------
# 1. Backup (must succeed before we touch anything).
# ---------------------------------------------------------------------------
if [[ ! -d "$DATA_DIR" ]]; then
  echo "Data directory $DATA_DIR not found - is Inventory Store installed?" >&2
  exit 1
fi

echo "Backing up $DATA_DIR ..."
mkdir -p "$BACKUP_DIR"

# Delete any previous update backup before taking a new one.
rm -f "$BACKUP_DIR"/inventorystore-data-*.tar.gz

BACKUP_FILE="$BACKUP_DIR/inventorystore-data-$(date +%Y%m%d-%H%M%S).tar.gz"
if ! tar czf "$BACKUP_FILE" -C "$(dirname "$DATA_DIR")" "$(basename "$DATA_DIR")"; then
  echo "Backup failed - aborting update. No changes were made." >&2
  rm -f "$BACKUP_FILE"
  exit 1
fi
# Verify the archive is readable; a corrupt backup is no backup.
if ! tar tzf "$BACKUP_FILE" >/dev/null 2>&1; then
  echo "Backup archive is unreadable - aborting update. No changes were made." >&2
  rm -f "$BACKUP_FILE"
  exit 1
fi
chown -R "$OWNER" "$BACKUP_DIR" 2>/dev/null || true
echo "Backup created: $BACKUP_FILE"

# ---------------------------------------------------------------------------
# 2. Find and download the latest release.
# ---------------------------------------------------------------------------
echo "Looking up the latest release of $REPO ..."
API="https://api.github.com/repos/$REPO/releases/latest"

if command -v jq >/dev/null 2>&1; then
  ASSET_URL="$(curl -fsSL "$API" \
    | jq -r '.assets[] | select(.name | test("linux-x64.*\\.tar\\.gz$")) | .browser_download_url' \
    | head -1)"
else
  ASSET_URL="$(curl -fsSL "$API" \
    | grep -o '"browser_download_url": *"[^"]*linux-x64[^"]*\.tar\.gz"' \
    | head -1 | sed 's/.*"\(https[^"]*\)".*/\1/')"
fi

if [[ -z "${ASSET_URL:-}" ]]; then
  echo "Could not find a linux-x64 tarball in the latest release." >&2
  exit 1
fi

TARBALL_NAME="$(basename "$ASSET_URL")"          # InventoryStore-linux-x64-<version>.tar.gz
VERSION_DIR="${TARBALL_NAME%.tar.gz}"            # InventoryStore-linux-x64-<version>
TARBALL_PATH="$HOME_DIR/$TARBALL_NAME"

echo "Downloading $TARBALL_NAME ..."
curl -fSL "$ASSET_URL" -o "$TARBALL_PATH"

# ---------------------------------------------------------------------------
# 3. Extract into ~ and run the bundled install.sh.
# ---------------------------------------------------------------------------
echo "Extracting into $HOME_DIR ..."
tar xzf "$TARBALL_PATH" -C "$HOME_DIR"

EXTRACT_DIR="$HOME_DIR/$VERSION_DIR"
if [[ ! -f "$EXTRACT_DIR/install.sh" ]]; then
  echo "Extracted release is missing install.sh at $EXTRACT_DIR." >&2
  exit 1
fi
chown -R "$OWNER" "$EXTRACT_DIR" 2>/dev/null || true

echo "Running installer ..."
chmod +x "$EXTRACT_DIR/install.sh"
( cd "$EXTRACT_DIR" && ./install.sh )

# ---------------------------------------------------------------------------
# 4. Cleanup once the service is back up.
# ---------------------------------------------------------------------------
echo "Waiting for $SERVICE to come back up ..."
for _ in $(seq 1 30); do
  if systemctl is-active --quiet "$SERVICE"; then
    break
  fi
  sleep 1
done

if ! systemctl is-active --quiet "$SERVICE"; then
  echo "Service did not come back up after the update." >&2
  echo "The tarball ($TARBALL_PATH) and extracted files were left in place for inspection." >&2
  echo "Your backup is safe at: $BACKUP_FILE" >&2
  exit 1
fi

echo "Service restarted. Cleaning up ..."
# Delete the downloaded tarball.
rm -f "$TARBALL_PATH"
# Delete older extracted version directories, keeping the one we just used.
for d in "$HOME_DIR"/InventoryStore-linux-x64-*; do
  [[ -d "$d" ]] || continue
  [[ "$d" == "$EXTRACT_DIR" ]] && continue
  rm -rf "$d"
done

echo
echo "Inventory Store updated to $VERSION_DIR and running."
echo "Backup of your previous data: $BACKUP_FILE"
