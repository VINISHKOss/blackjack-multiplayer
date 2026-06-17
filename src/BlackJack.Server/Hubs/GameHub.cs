using BlackJack.Server.Services;
using BlackJack.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace BlackJack.Server.Hubs;

public sealed class GameHub : Hub
{
    private readonly GameService _gameService;
    private readonly TableStateMapper _mapper;

    public GameHub(GameService gameService, TableStateMapper mapper)
    {
        _gameService = gameService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TableSummary>> GetTables()
    {
        return _gameService.GetTableSummaries();
    }

    public async Task<TableStateDto?> CreateTable(string tableName, string playerName)
    {
        var result = _gameService.CreateTable(tableName, playerName, Context.ConnectionId);
        if (!result.Success || result.Table is null)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return null;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, result.Table.Id);
        var dto = _mapper.ToDto(result.Table);
        await Clients.Group(result.Table.Id).SendAsync("TableUpdated", dto);
        await BroadcastTableList();
        return dto;
    }

    public async Task<TableStateDto?> JoinTable(string tableId, string playerName)
    {
        var result = _gameService.JoinTable(tableId, playerName, Context.ConnectionId);
        if (!result.Success || result.Table is null)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return null;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, tableId);
        var dto = _mapper.ToDto(result.Table);
        await Clients.Group(tableId).SendAsync("TableUpdated", dto);
        await BroadcastTableList();
        return dto;
    }

    public async Task ReconnectToTable(string tableId, string playerId)
    {
        var result = _gameService.Reconnect(tableId, playerId, Context.ConnectionId);
        if (!result.Success || result.Table is null)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, tableId);
        var dto = _mapper.ToDto(result.Table);
        await Clients.Caller.SendAsync("TableUpdated", dto);
    }

    public async Task LeaveTable()
    {
        var table = _gameService.GetTableByConnection(Context.ConnectionId);
        if (table is null)
            return;

        var tableId = table.Id;
        var result = _gameService.LeaveTable(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tableId);

        if (result.Table is not null)
        {
            var dto = _mapper.ToDto(result.Table);
            await Clients.Group(tableId).SendAsync("TableUpdated", dto);
        }

        await BroadcastTableList();
    }

    public async Task PlaceBet(int amount)
    {
        var table = _gameService.GetTableByConnection(Context.ConnectionId);
        if (table is null)
        {
            await Clients.Caller.SendAsync("Error", "Вы не за столом.");
            return;
        }

        var result = _gameService.PlaceBet(Context.ConnectionId, amount);
        if (!result.Success)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        await BroadcastTableState(table.Id);
        await RunDealerAnimationIfNeeded(table.Id);
    }

    public async Task PlayerAction(GameAction action)
    {
        var table = _gameService.GetTableByConnection(Context.ConnectionId);
        if (table is null)
        {
            await Clients.Caller.SendAsync("Error", "Вы не за столом.");
            return;
        }

        var result = _gameService.PlayerAction(Context.ConnectionId, action);
        if (!result.Success)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        await BroadcastTableState(table.Id);
        await RunDealerAnimationIfNeeded(table.Id);
    }

    public async Task RetryAfterBankrupt()
    {
        var result = _gameService.RetryAfterBankrupt(Context.ConnectionId);
        if (!result.Success || result.Table is null)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        await BroadcastTableState(result.Table.Id);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var table = _gameService.GetTableByConnection(Context.ConnectionId);
        if (table is not null)
        {
            var player = table.GetPlayerByConnection(Context.ConnectionId);
            if (player is not null)
            {
                player.IsConnected = false;
                await BroadcastTableState(table.Id);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RunDealerAnimationIfNeeded(string tableId)
    {
        if (_gameService.NeedsDealerAnimation(tableId))
            await _gameService.RunDealerAnimationAsync(tableId, BroadcastTableState);
    }

    private async Task BroadcastTableState(string tableId)
    {
        var table = _gameService.GetTableDto(tableId);
        if (table is not null)
            await Clients.Group(tableId).SendAsync("TableUpdated", table);
    }

    private async Task BroadcastTableList()
    {
        var tables = _gameService.GetTableSummaries();
        await Clients.All.SendAsync("TablesUpdated", tables);
    }
}