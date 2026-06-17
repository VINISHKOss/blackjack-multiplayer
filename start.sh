#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

PORT=5134
echo "Останавливаем старый сервер..."
lsof -ti :"$PORT" | xargs kill -9 2>/dev/null || true
sleep 1

echo "Сборка проекта..."
DOTNET_SYSTEM_NET_HTTP_USEPROXY=0 dotnet build src/BlackJack.Server/BlackJack.Server.csproj --configfile NuGet.Config -v q

echo "Запуск сервера на http://localhost:$PORT ..."
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_SYSTEM_NET_HTTP_USEPROXY=0
dotnet run --project src/BlackJack.Server/BlackJack.Server.csproj \
  --configfile NuGet.Config \
  --no-launch-profile \
  --urls "http://localhost:$PORT"