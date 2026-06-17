FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/BlackJack.Server/BlackJack.Server.csproj src/BlackJack.Server/
COPY src/BlackJack.Client/BlackJack.Client.csproj src/BlackJack.Client/
COPY src/BlackJack.Shared/BlackJack.Shared.csproj src/BlackJack.Shared/

RUN dotnet restore src/BlackJack.Server/BlackJack.Server.csproj \
    --source https://api.nuget.org/v3/index.json

COPY src/ src/
RUN dotnet publish src/BlackJack.Server/BlackJack.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 10000

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BlackJack.Server.dll"]