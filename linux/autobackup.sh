#!/usr/bin/env bash
#
# Takes a single data-only backup of Inventory Store into ~/backups/autobackups,
# deleting the previous nightly backup first so only the latest is kept.
#
# This is the worker run by the inventorystore-backup.timer (installed by
# setup-autobackup.sh). It can also be run by hand:
#   sudo ./autobackup.sh
#
# The target home directory is taken from $BACKUP_HOME when set (the systemd
# service sets it); otherwise it is resolved from the invoking user.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi

set -euo pipefail

DATA_DIR=/var/lib/inventorystore

if [[ $EUID -ne 0 ]]; then
  echo "Please run as root: sudo ./autobackup.sh" >&2
  exit 1
fi

if [[ -n "${BACKUP_HOME:-}" ]]; then
  HOME_DIR="$BACKUP_HOME"
  OWNER="$(stat -c '%U' "$BACKUP_HOME" 2>/dev/null || id -un)"
elif [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != "root" ]]; then
  HOME_DIR="$(getent passwd "$SUDO_USER" | cut -d: -f6)"
  OWNER="$SUDO_USER"
else
  HOME_DIR="${HOME:-/root}"
  OWNER="$(id -un)"
fi

AUTO_DIR="$HOME_DIR/backups/autobackups"

if [[ ! -d "$DATA_DIR" ]]; then
  echo "Data directory $DATA_DIR not found - is Inventory Store installed?" >&2
  exit 1
fi

mkdir -p "$AUTO_DIR"

BACKUP_FILE="$AUTO_DIR/inventorystore-autobackup-$(date +%Y%m%d-%H%M%S).tar.gz"
if ! tar czf "$BACKUP_FILE" -C "$(dirname "$DATA_DIR")" "$(basename "$DATA_DIR")"; then
  echo "Nightly backup failed." >&2
  rm -f "$BACKUP_FILE"
  exit 1
fi
if ! tar tzf "$BACKUP_FILE" >/dev/null 2>&1; then
  echo "Nightly backup archive is unreadable." >&2
  rm -f "$BACKUP_FILE"
  exit 1
fi

# Delete the stale backup(s) now that a good fresh one exists, keeping only the
# one we just created.
for f in "$AUTO_DIR"/inventorystore-autobackup-*.tar.gz; do
  [[ -f "$f" ]] || continue
  [[ "$f" == "$BACKUP_FILE" ]] && continue
  rm -f "$f"
done

chown -R "$OWNER" "$HOME_DIR/backups" 2>/dev/null || true
echo "Nightly backup created: $BACKUP_FILE"
