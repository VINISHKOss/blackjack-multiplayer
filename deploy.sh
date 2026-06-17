#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

if [[ -z "${RENDER_API_KEY:-}" ]]; then
  echo "Нужен API-ключ Render: https://dashboard.render.com/u/settings#api-keys"
  echo "export RENDER_API_KEY=rnd_..."
  exit 1
fi

if ! command -v render >/dev/null 2>&1; then
  echo "Установите Render CLI: brew install render"
  exit 1
fi

echo "Деплой на Render (blackjack-multiplayer.onrender.com)..."
render blueprint apply render.yaml --confirm

echo ""
echo "Готово. URL появится в панели Render через 3–5 минут."