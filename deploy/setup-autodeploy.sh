#!/usr/bin/env bash
#
# Turns on automatic installs of new GitHub releases for one app on this server.
# Run it from the extracted release bundle, after install.sh.
#
#   sudo ./setup-autodeploy.sh                         ask for repo, channel and token
#   sudo ./setup-autodeploy.sh --channel prerelease    switch channel, keeps the token
#   sudo ./setup-autodeploy.sh --token-from /etc/other-app/autoupdate.env   reuse a token
#   sudo ./setup-autodeploy.sh --disable               remove the timer and config
#
# Non-interactive: GITHUB_REPO, GITHUB_TOKEN and CHANNEL may be set in the environment.
# The token is a fine-grained, read-only (Contents) personal access token. It is stored
# root-only (0600) in /etc/<slug>/autoupdate.env and never printed.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/app.conf"
: "${APP_SLUG:?}"
SERVICE_NAME="${SERVICE_NAME:-$APP_SLUG}"
UPDATE_MINUTE_OFFSET="${UPDATE_MINUTE_OFFSET:-0}"
ASSET_PATTERN="${ASSET_PATTERN:-linux-x64.tar.gz}"

WORKER="/usr/local/bin/$APP_SLUG-autoupdate"
CONFIG_DIR="/etc/$APP_SLUG"
APP_CONF_INSTALLED="$CONFIG_DIR/app.conf"
ENV_FILE="$CONFIG_DIR/autoupdate.env"
SERVICE_UNIT="/etc/systemd/system/$APP_SLUG-autoupdate.service"
TIMER_UNIT="/etc/systemd/system/$APP_SLUG-autoupdate.timer"

[ "$EUID" -eq 0 ] || { echo "Please run as root." >&2; exit 1; }

# shellcheck disable=SC1090
read_env_value() { (set +u; source "$1" 2>/dev/null; eval "printf '%s' \"\${$2:-}\""); }

set_channel() {
    local channel="$1"
    case "$channel" in stable|prerelease) ;; *) echo "Channel must be stable or prerelease." >&2; exit 1 ;; esac
    [ -f "$ENV_FILE" ] || { echo "$ENV_FILE does not exist yet; run without --channel first." >&2; exit 1; }
    if grep -q '^CHANNEL=' "$ENV_FILE"; then
        sed -i "s/^CHANNEL=.*/CHANNEL=\"$channel\"/" "$ENV_FILE"
    else
        printf 'CHANNEL="%s"\n' "$channel" >> "$ENV_FILE"
    fi
    echo "$APP_SLUG now follows the $channel channel."
}

case "${1:-}" in
    --disable)
        systemctl disable --now "$APP_SLUG-autoupdate.timer" 2>/dev/null || true
        rm -f "$SERVICE_UNIT" "$TIMER_UNIT" "$ENV_FILE" "$WORKER"
        systemctl daemon-reload
        echo "Automatic deployment disabled."
        exit 0 ;;
    --channel)
        set_channel "${2:?usage: --channel stable|prerelease}"
        exit 0 ;;
    --token-from)
        GITHUB_TOKEN="$(read_env_value "${2:?usage: --token-from <autoupdate.env>}" GITHUB_TOKEN)"
        [ -n "$GITHUB_TOKEN" ] || { echo "No GITHUB_TOKEN found in $2." >&2; exit 1; } ;;
esac

if [ -f "$ENV_FILE" ] && [ -z "${GITHUB_REPO:-}${GITHUB_TOKEN:-}" ]; then
    echo "Already configured ($ENV_FILE exists). Use --channel to switch channel or --disable first to start over."
    exit 0
fi

command -v jq >/dev/null 2>&1 || {
    echo "jq is required. Installing..."
    apt-get update -qq && apt-get install -y -qq jq || { echo "Install jq and re-run." >&2; exit 1; }
}
command -v flock >/dev/null 2>&1 || { echo "flock (util-linux) is required." >&2; exit 1; }

if [ -z "${GITHUB_REPO:-}" ]; then
    read -r -p "GitHub repo to watch (owner/repo) [JTCozart/$APP_SLUG]: " GITHUB_REPO
    GITHUB_REPO="${GITHUB_REPO:-JTCozart/$APP_SLUG}"
fi
if [ -z "${CHANNEL:-}" ]; then
    read -r -p "Channel for this server, stable or prerelease [stable]: " CHANNEL
    CHANNEL="${CHANNEL:-stable}"
fi
case "$CHANNEL" in stable|prerelease) ;; *) echo "Channel must be stable or prerelease." >&2; exit 1 ;; esac
if [ -z "${GITHUB_TOKEN:-}" ]; then
    read -r -s -p "Fine-grained read-only GitHub token for $GITHUB_REPO: " GITHUB_TOKEN
    echo
fi
[ -n "$GITHUB_TOKEN" ] || { echo "A token is required." >&2; exit 1; }

echo "Verifying access..."
curl -sf -H "Authorization: Bearer $GITHUB_TOKEN" -H "Accept: application/vnd.github+json" \
    "https://api.github.com/repos/$GITHUB_REPO" >/dev/null \
    || { echo "Could not read $GITHUB_REPO with that token. Check the name and the token's permissions." >&2; exit 1; }

install -m 0755 "$SCRIPT_DIR/autoupdate.sh" "$WORKER"
mkdir -p "$CONFIG_DIR"
install -m 0644 "$SCRIPT_DIR/app.conf" "$APP_CONF_INSTALLED"

umask 077
cat > "$ENV_FILE" <<EOF
GITHUB_REPO="$GITHUB_REPO"
GITHUB_TOKEN="$GITHUB_TOKEN"
CHANNEL="$CHANNEL"
ASSET_PATTERN="$ASSET_PATTERN"
EOF
chmod 0600 "$ENV_FILE"
umask 022

cat > "$SERVICE_UNIT" <<EOF
[Unit]
Description=$APP_SLUG - check for and install new GitHub releases
After=network-online.target $SERVICE_NAME.service
Wants=network-online.target
ConditionPathExists=$ENV_FILE

[Service]
Type=oneshot
Environment=APP_CONF=$APP_CONF_INSTALLED
ExecStart=$WORKER
TimeoutStartSec=3600
EOF

cat > "$TIMER_UNIT" <<EOF
[Unit]
Description=Every 10 minutes, check for new $APP_SLUG releases

[Timer]
OnCalendar=*:$UPDATE_MINUTE_OFFSET/10
RandomizedDelaySec=30
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now "$APP_SLUG-autoupdate.timer"

cat <<EOF

Automatic deployment is on for $APP_SLUG: repo $GITHUB_REPO, channel $CHANNEL, checked at
minute $UPDATE_MINUTE_OFFSET of every 10 minutes.

  systemctl list-timers $APP_SLUG-autoupdate.timer
  systemctl start $APP_SLUG-autoupdate.service         check and install right now
  APP_CONF=$APP_CONF_INSTALLED $WORKER --check          report only
  journalctl -u $APP_SLUG-autoupdate.service -f
  ./setup-autodeploy.sh --channel stable|prerelease     switch channel
EOF
