#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

RENDER_BIN="${RENDER_BIN:-$(dirname "$0")/.tools/render}"
if [[ ! -x "$RENDER_BIN" ]]; then
  RENDER_BIN="$(command -v render || true)"
fi

if [[ -z "${RENDER_API_KEY:-}" ]]; then
  echo "Нужен API-ключ Render: https://dashboard.render.com/u/settings#api-keys"
  echo "export RENDER_API_KEY=rnd_..."
  exit 1
fi

export RENDER_API_KEY

if [[ -z "${RENDER_OWNER_ID:-}" ]]; then
  echo "Нужен ID workspace Render (Settings → Workspace ID):"
  echo "export RENDER_OWNER_ID=tea_..."
  exit 1
fi

if [[ -z "$RENDER_BIN" || ! -x "$RENDER_BIN" ]]; then
  echo "Render CLI не найден. Скачайте: https://github.com/render-oss/cli/releases"
  exit 1
fi

"$RENDER_BIN" blueprints validate render.yaml --confirm

echo "Создаём сервис blackjack-multiplayer на Render..."
curl -fsS -X POST "https://api.render.com/v1/services" \
  -H "Authorization: Bearer ${RENDER_API_KEY}" \
  -H "Content-Type: application/json" \
  -d @- <<EOF
{
  "type": "web_service",
  "name": "blackjack-multiplayer",
  "ownerId": "${RENDER_OWNER_ID}",
  "repo": "${GITHUB_REPO:-}",
  "autoDeploy": "yes",
  "branch": "main",
  "serviceDetails": {
    "env": "docker",
    "plan": "free",
    "region": "frankfurt",
    "healthCheckPath": "/",
    "envSpecificDetails": {
      "dockerfilePath": "./Dockerfile",
      "dockerContext": "."
    }
  },
  "envVars": [
    { "key": "ASPNETCORE_ENVIRONMENT", "value": "Production" }
  ]
}
EOF

echo ""
echo "Готово. URL: https://blackjack-multiplayer.onrender.com (через 3–5 мин после сборки)"