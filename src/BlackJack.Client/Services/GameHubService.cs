using BlackJack.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlackJack.Client.Services;

public sealed class GameHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly NavigationManager _navigation;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public string? PlayerId { get; set; }
    public string? TableId { get; private set; }
    public string PlayerName { get; set; } = $"Player{Random.Shared.Next(100, 999)}";

    public event Action<TableStateDto>? TableUpdated;
    public event Action<IReadOnlyList<TableSummary>>? TablesUpdated;
    public event Action<string>? ErrorReceived;
    public event Action? ConnectionStateChanged;

    public GameHubService(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
            return;

        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_navigation.ToAbsoluteUri("/gamehub"))
                .WithAutomaticReconnect()
                .Build();

            _connection.On<TableStateDto>("TableUpdated", dto =>
            {
                TableId ??= dto.Id;
                var me = dto.Players.FirstOrDefault(p => p.Name == PlayerName);
                if (me is not null)
                    PlayerId = me.Id;
                TableUpdated?.Invoke(dto);
            });

            _connection.On<IReadOnlyList<TableSummary>>("TablesUpdated", tables =>
                TablesUpdated?.Invoke(tables));

            _connection.On<string>("Error", msg => ErrorReceived?.Invoke(msg));

            _connection.Reconnecting += _ =>
            {
                ConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };

            _connection.Reconnected += _ =>
            {
                ConnectionStateChanged?.Invoke();
                if (TableId is not null && PlayerId is not null)
                    return _connection.InvokeAsync("ReconnectToTable", TableId, PlayerId);
                return Task.CompletedTask;
            };

            _connection.Closed += _ =>
            {
                ConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };
        }

        await _connection.StartAsync();
        ConnectionStateChanged?.Invoke();
    }

    public async Task<IReadOnlyList<TableSummary>> GetTablesAsync()
    {
        await EnsureConnectedAsync();
        return await _connection!.InvokeAsync<IReadOnlyList<TableSummary>>("GetTables");
    }

    public async Task<TableStateDto?> CreateTableAsync(string tableName)
    {
        await EnsureConnectedAsync();
        var dto = await _connection!.InvokeAsync<TableStateDto?>("CreateTable", tableName, PlayerName);
        if (dto is not null)
        {
            TableId = dto.Id;
            PlayerId = dto.Players.FirstOrDefault(p => p.Name == PlayerName)?.Id;
        }
        return dto;
    }

    public async Task<TableStateDto?> JoinTableAsync(string tableId)
    {
        await EnsureConnectedAsync();
        var dto = await _connection!.InvokeAsync<TableStateDto?>("JoinTable", tableId, PlayerName);
        if (dto is not null)
        {
            TableId = tableId;
            PlayerId = dto.Players.FirstOrDefault(p => p.Name == PlayerName)?.Id;
        }
        return dto;
    }

    public Task PlaceBetAsync(int amount) =>
        _connection!.InvokeAsync("PlaceBet", amount);

    public Task PlayerActionAsync(GameAction action) =>
        _connection!.InvokeAsync("PlayerAction", action);

    public Task RetryAfterBankruptAsync() =>
        _connection!.InvokeAsync("RetryAfterBankrupt");

    public async Task LeaveTableAsync()
    {
        if (_connection is not null)
            await _connection.InvokeAsync("LeaveTable");
        TableId = null;
        PlayerId = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}