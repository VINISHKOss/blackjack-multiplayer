#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

: "${GITHUB_TOKEN:?Нужен GITHUB_TOKEN}"
: "${RENDER_API_KEY:?Нужен RENDER_API_KEY}"
: "${RENDER_OWNER_ID:?Нужен RENDER_OWNER_ID}"

GH="${GH_BIN:-$(dirname "$0")/.tools/gh}"
export GH_TOKEN="$GITHUB_TOKEN"

if [ ! -x "$GH" ]; then
  echo "GitHub CLI не найден в .tools/gh"
  exit 1
fi

"$GH" auth setup-git

if ! "$GH" repo view VINISHKOss/blackjack-multiplayer >/dev/null 2>&1; then
  echo "Создайте пустой публичный репозиторий:"
  echo "https://github.com/new?name=blackjack-multiplayer"
  exit 1
fi

git remote remove origin 2>/dev/null || true
git remote add origin "https://github.com/VINISHKOss/blackjack-multiplayer.git"
git push -u origin main

"$GH" secret set RENDER_API_KEY --body "$RENDER_API_KEY" --repo VINISHKOss/blackjack-multiplayer
"$GH" secret set RENDER_OWNER_ID --body "$RENDER_OWNER_ID" --repo VINISHKOss/blackjack-multiplayer

"$GH" workflow run deploy-render.yml --repo VINISHKOss/blackjack-multiplayer

echo ""
echo "Деплой запущен. Сайт: https://blackjack-multiplayer.onrender.com"