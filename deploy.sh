#!/usr/bin/env bash
#
# Deploy the Knowledge Center API on MRTN-LAPPS.
#
# Run this ON the host (matt@10.0.0.232) after pushing to origin/main:
#
#     cd ~/Knowledge-Center-API && ./deploy.sh
#
# It fast-forwards the checkout, republishes into bin/publish, restarts the
# systemd unit, and health-checks it. If the health check fails it prints the
# journal tail and the commit to roll back to; it does NOT roll back on its
# own, so you stay in control of what ends up live.

set -euo pipefail

REPO_DIR="${REPO_DIR:-/home/matt/Knowledge-Center-API}"
SERVICE="${SERVICE:-knowledge-center-api.service}"
# The one endpoint that needs no auth. UsePathBase("/kc") is why the path is
# prefixed, and the port matches the Kestrel binding in the unit file.
HEALTH_URL="${HEALTH_URL:-http://localhost:5065/kc/api/auth/demo}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-30}"

export DOTNET_ROOT="${DOTNET_ROOT:-/home/matt/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

cd "$REPO_DIR"

previous="$(git rev-parse --short HEAD)"
echo "==> Current commit: $previous"

echo "==> Pulling origin/main"
git pull --ff-only

current="$(git rev-parse --short HEAD)"
if [ "$current" = "$previous" ]; then
    echo "    Already at $current — republishing anyway"
else
    echo "    $previous -> $current"
fi

echo "==> Publishing (Release)"
dotnet publish -c Release -o bin/publish --nologo -v minimal

echo "==> Restarting $SERVICE"
sudo systemctl restart "$SERVICE"

echo "==> Waiting for health check ($HEALTH_URL)"
deadline=$((SECONDS + HEALTH_TIMEOUT))
while [ "$SECONDS" -lt "$deadline" ]; do
    code="$(curl -s -o /dev/null -w '%{http_code}' -X POST "$HEALTH_URL" || true)"
    if [ "$code" = "200" ]; then
        echo "    OK (HTTP 200)"
        echo
        echo "Deployed $current to $SERVICE."
        exit 0
    fi
    sleep 1
done

echo "    FAILED — last status: ${code:-no response}" >&2
echo >&2
echo "--- journalctl -u $SERVICE (last 30 lines) ---" >&2
journalctl -u "$SERVICE" --no-pager -n 30 >&2
echo >&2
echo "To roll back:" >&2
echo "    cd $REPO_DIR && git reset --hard $previous && ./deploy.sh" >&2
exit 1
