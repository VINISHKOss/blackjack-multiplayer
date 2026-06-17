using System.Collections.Concurrent;
using BlackJack.Shared.Models;

namespace BlackJack.Server.Services;

public sealed class GameService
{
    private const int HoleCardFlipDelayMs = 900;
    private const int DealerDrawDelayMs = 700;
    private const int PreFlipPauseMs = 500;

    private readonly ConcurrentDictionary<string, TableState> _tables = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _dealerLocks = new();
    private readonly BlackjackRulesService _rules;
    private readonly TableStateMapper _mapper;

    public GameService(BlackjackRulesService rules, TableStateMapper mapper)
    {
        _rules = rules;
        _mapper = mapper;
    }

    public IReadOnlyList<TableSummary> GetTableSummaries() =>
        _tables.Values
            .OrderByDescending(t => t.CreatedAt)
            .Select(_mapper.ToSummary)
            .ToList();

    public (bool Success, string? Error, TableState? Table) CreateTable(string tableName, string playerName, string connectionId)
    {
        var table = new TableState
        {
            Name = string.IsNullOrWhiteSpace(tableName) ? "Blackjack Table" : tableName.Trim()
        };

        var joinResult = TryJoinTableInternal(table, playerName, connectionId, isNewTable: true);
        if (!joinResult.Success)
            return (false, joinResult.Error, null);

        _tables[table.Id] = table;
        return (true, null, table);
    }

    public (bool Success, string? Error, TableState? Table) JoinTable(string tableId, string playerName, string connectionId)
    {
        if (!_tables.TryGetValue(tableId, out var table))
            return (false, "Стол не найден.", null);

        return TryJoinTableInternal(table, playerName, connectionId, isNewTable: false);
    }

    private (bool Success, string? Error, TableState? Table) TryJoinTableInternal(
        TableState table, string playerName, string connectionId, bool isNewTable)
    {
        var existing = table.GetPlayerByConnection(connectionId);
        if (existing is not null)
        {
            existing.IsConnected = true;
            existing.Name = SanitizeName(playerName, existing.Name);
            return (true, null, table);
        }

        if (table.Players.Count(p => p.IsConnected) >= TableState.MaxPlayers)
            return (false, "Стол заполнен (максимум 5 игроков).", null);

        if (table.Phase is not (GamePhase.WaitingForPlayers or GamePhase.Betting or GamePhase.RoundComplete))
            return (false, "Нельзя присоединиться во время раунда.", null);

        var seat = FindFreeSeat(table);
        var player = new Player
        {
            ConnectionId = connectionId,
            Name = SanitizeName(playerName),
            SeatIndex = seat,
            Chips = Player.StartingChips,
            IsConnected = true
        };

        table.Players.Add(player);
        table.LastEventMessage = $"{player.Name} присоединился к столу.";

        if (table.Phase == GamePhase.WaitingForPlayers && table.Players.Count(p => p.IsConnected) >= 1)
            table.Phase = GamePhase.Betting;

        return (true, null, table);
    }

    public (bool Success, string? Error, TableState? Table) LeaveTable(string connectionId)
    {
        var table = FindTableByConnection(connectionId);
        if (table is null)
            return (false, "Вы не за столом.", null);

        var player = table.GetPlayerByConnection(connectionId);
        if (player is null)
            return (false, "Игрок не найден.", null);

        player.IsConnected = false;
        player.ConnectionId = string.Empty;
        table.LastEventMessage = $"{player.Name} покинул стол.";

        if (table.Players.All(p => !p.IsConnected))
            _tables.TryRemove(table.Id, out _);

        return (true, null, table);
    }

    public (bool Success, string? Error, TableState? Table) Reconnect(string tableId, string playerId, string connectionId)
    {
        if (!_tables.TryGetValue(tableId, out var table))
            return (false, "Стол не найден.", null);

        var player = table.GetPlayer(playerId);
        if (player is null)
            return (false, "Игрок не найден.", null);

        player.ConnectionId = connectionId;
        player.IsConnected = true;
        return (true, null, table);
    }

    public TableState? GetTableByConnection(string connectionId) =>
        FindTableByConnection(connectionId);

    public TableStateDto? GetTableDto(string tableId, bool revealDealer = false)
    {
        if (!_tables.TryGetValue(tableId, out var table))
            return null;

        var reveal = revealDealer || table.Phase is GamePhase.DealerTurn or GamePhase.Settlement or GamePhase.RoundComplete;
        return _mapper.ToDto(table, reveal);
    }

    public (bool Success, string? Error) PlaceBet(string connectionId, int amount)
    {
        var table = FindTableByConnection(connectionId);
        if (table is null) return (false, "Вы не за столом.");
        if (table.Phase is GamePhase.RoundComplete)
            PrepareNextBettingRound(table);

        if (table.Phase != GamePhase.Betting) return (false, "Сейчас не фаза ставок.");

        var player = table.GetPlayerByConnection(connectionId);
        if (player is null) return (false, "Игрок не найден.");
        if (player.IsBankrupt) return (false, "Вы банкрот. Нажмите «Попробовать снова».");
        if (amount <= 0) return (false, "Ставка должна быть больше 0.");
        if (amount > player.Chips) return (false, "Недостаточно фишек.");

        player.CurrentBet = amount;
        player.HasPlacedBet = true;
        player.LastActionMessage = $"Ставка: {amount}";

        var activeBettors = table.Players.Where(p => p.IsConnected && !p.IsBankrupt && p.Chips > 0).ToList();
        if (activeBettors.Count > 0 && activeBettors.All(p => p.HasPlacedBet))
            StartRound(table);

        return (true, null);
    }

    public bool NeedsDealerAnimation(string tableId) =>
        _tables.TryGetValue(tableId, out var table) && table.DealerAnimationPending;

    public async Task RunDealerAnimationAsync(string tableId, Func<string, Task> broadcast)
    {
        if (!_tables.TryGetValue(tableId, out var table))
            return;

        var gate = _dealerLocks.GetOrAdd(tableId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0))
            return;

        try
        {
            if (!table.DealerAnimationPending)
                return;

            table.DealerAnimationPending = false;

            await broadcast(tableId);
            await Task.Delay(PreFlipPauseMs);

            if (table.Dealer.Cards.Count >= 2)
                table.Dealer.Cards[1].IsFaceDown = false;

            table.LastEventMessage = "Дилер открывает карту...";
            await broadcast(tableId);
            await Task.Delay(HoleCardFlipDelayMs);

            while (_rules.DealerShouldHit(table.Dealer.Cards))
            {
                table.Dealer.Cards.Add(Draw(table));
                table.LastEventMessage = "Дилер берёт карту...";
                await broadcast(tableId);
                await Task.Delay(DealerDrawDelayMs);
            }

            table.Dealer.Status = _rules.IsBusted(table.Dealer.Cards)
                ? HandStatus.Busted
                : HandStatus.Standing;

            SettleRound(table);
            await broadcast(tableId);
        }
        finally
        {
            gate.Release();
        }
    }

    public (bool Success, string? Error) PlayerAction(string connectionId, GameAction action)
    {
        var table = FindTableByConnection(connectionId);
        if (table is null) return (false, "Вы не за столом.");
        if (table.Phase is GamePhase.DealerTurn or GamePhase.Settlement)
            return (false, "Дождитесь окончания хода дилера.");
        if (table.Phase != GamePhase.PlayerTurn) return (false, "Сейчас не ваш ход.");

        var player = table.GetPlayerByConnection(connectionId);
        if (player is null) return (false, "Игрок не найден.");
        if (player.Id != table.ActivePlayerId) return (false, "Сейчас не ваш ход.");

        var hand = GetActiveHand(player);
        if (hand is null) return (false, "Нет активной руки.");

        return action switch
        {
            GameAction.Hit => Hit(table, player, hand),
            GameAction.Stand => Stand(table, player, hand),
            GameAction.Double => Double(table, player, hand),
            GameAction.Split => Split(table, player, hand),
            _ => (false, "Неизвестное действие.")
        };
    }

    public (bool Success, string? Error, TableState? Table) RetryAfterBankrupt(string connectionId)
    {
        var table = FindTableByConnection(connectionId);
        if (table is null) return (false, "Вы не за столом.", null);

        var player = table.GetPlayerByConnection(connectionId);
        if (player is null) return (false, "Игрок не найден.", null);
        if (!player.IsBankrupt) return (false, "Вы не банкрот.", null);
        if (table.Phase is GamePhase.PlayerTurn or GamePhase.DealerTurn or GamePhase.Dealing)
            return (false, "Дождитесь окончания раунда.", null);

        player.Chips = Player.StartingChips;
        player.IsBankrupt = false;
        player.HasPlacedBet = false;
        player.CurrentBet = 0;
        player.Hands.Clear();
        player.LastActionMessage = "Получил 1000 фишек!";
        table.LastEventMessage = $"{player.Name} снова в игре с {Player.StartingChips} фишками!";

        if (table.Phase is GamePhase.RoundComplete or GamePhase.WaitingForPlayers)
            PrepareNextBettingRound(table);

        return (true, null, table);
    }

    public (bool Success, string? Error) StartBettingRound(string connectionId)
    {
        var table = FindTableByConnection(connectionId);
        if (table is null) return (false, "Вы не за столом.");
        if (table.Phase is not (GamePhase.RoundComplete or GamePhase.Betting))
            return (false, "Нельзя начать ставки сейчас.");

        PrepareNextBettingRound(table);
        return (true, null);
    }

    private void StartRound(TableState table)
    {
        table.Phase = GamePhase.Dealing;
        table.RoundNumber++;
        table.Deck = _rules.CreateShoe();
        table.Dealer = new Dealer();
        table.LastEventMessage = "Раздача карт...";

        foreach (var player in table.Players.Where(p => p.IsConnected && p.HasPlacedBet))
        {
            player.Hands =
            [
                new PlayerHand
                {
                    Bet = player.CurrentBet,
                    Cards = [Draw(table), Draw(table)]
                }
            ];
            player.CurrentHandIndex = 0;
            player.Chips -= player.CurrentBet;

            if (_rules.IsBlackjack(player.Hands[0].Cards))
                player.Hands[0].Status = HandStatus.Blackjack;
        }

        table.Dealer.Cards.Add(Draw(table));
        var holeCard = Draw(table);
        holeCard.IsFaceDown = true;
        table.Dealer.Cards.Add(holeCard);

        if (_rules.IsBlackjack(table.Dealer.Cards))
        {
            table.Dealer.Status = HandStatus.Blackjack;
            BeginDealerTurn(table, "У дилера блэкджек...");
            return;
        }

        table.Phase = GamePhase.PlayerTurn;
        AdvanceToNextPlayer(table);
    }

    private (bool Success, string? Error) Hit(TableState table, Player player, PlayerHand hand)
    {
        hand.Cards.Add(Draw(table));
        player.LastActionMessage = "Hit";

        if (_rules.IsBusted(hand.Cards))
        {
            hand.Status = HandStatus.Busted;
            AdvanceHandOrPlayer(table, player);
        }

        return (true, null);
    }

    private (bool Success, string? Error) Stand(TableState table, Player player, PlayerHand hand)
    {
        hand.Status = HandStatus.Standing;
        player.LastActionMessage = "Stand";
        AdvanceHandOrPlayer(table, player);
        return (true, null);
    }

    private (bool Success, string? Error) Double(TableState table, Player player, PlayerHand hand)
    {
        if (!_rules.CanDouble(player, hand))
            return (false, "Double недоступен.");

        player.Chips -= hand.Bet;
        hand.Bet *= 2;
        hand.Cards.Add(Draw(table));
        hand.Status = _rules.IsBusted(hand.Cards) ? HandStatus.Busted : HandStatus.Doubled;
        player.LastActionMessage = "Double";

        AdvanceHandOrPlayer(table, player);
        return (true, null);
    }

    private (bool Success, string? Error) Split(TableState table, Player player, PlayerHand hand)
    {
        if (!_rules.CanSplit(hand))
            return (false, "Split недоступен.");
        if (player.Chips < hand.Bet)
            return (false, "Недостаточно фишек для Split.");

        player.Chips -= hand.Bet;
        var card1 = hand.Cards[0];
        var card2 = hand.Cards[1];

        hand.Cards = [card1, Draw(table)];
        hand.IsSplitHand = true;
        hand.Status = HandStatus.Active;

        var secondHand = new PlayerHand
        {
            Bet = hand.Bet,
            Cards = [card2, Draw(table)],
            IsSplitHand = true
        };

        player.Hands.Add(secondHand);
        player.LastActionMessage = "Split";
        return (true, null);
    }

    private void AdvanceHandOrPlayer(TableState table, Player player)
    {
        if (HasActiveHand(player))
            return;

        AdvanceToNextPlayer(table);
    }

    private void AdvanceToNextPlayer(TableState table)
    {
        var activePlayers = table.Players
            .Where(p => p.IsConnected && p.HasPlacedBet && p.Hands.Count > 0)
            .OrderBy(p => p.SeatIndex)
            .ToList();

        var startIndex = table.ActivePlayerId is null
            ? 0
            : activePlayers.FindIndex(p => p.Id == table.ActivePlayerId) + 1;

        for (var i = startIndex; i < activePlayers.Count; i++)
        {
            var candidate = activePlayers[i];
            if (candidate.Hands.All(h => h.Status is HandStatus.Blackjack or HandStatus.Busted or HandStatus.Standing or HandStatus.Doubled))
                continue;

            candidate.CurrentHandIndex = FindNextActiveHandIndex(candidate);
            if (candidate.CurrentHandIndex >= 0)
            {
                table.ActivePlayerId = candidate.Id;
                table.CurrentPlayerIndex = i;
                table.LastEventMessage = $"Ход игрока {candidate.Name}";
                return;
            }
        }

        BeginDealerTurn(table);
    }

    private void BeginDealerTurn(TableState table, string? message = null)
    {
        table.Phase = GamePhase.DealerTurn;
        table.ActivePlayerId = null;
        table.DealerAnimationPending = true;
        table.LastEventMessage = message ?? "Дилер открывает карту...";
    }

    private void SettleRound(TableState table)
    {
        table.Phase = GamePhase.Settlement;

        foreach (var player in table.Players.Where(p => p.HasPlacedBet))
        {
            foreach (var hand in player.Hands)
            {
                if (hand.Status == HandStatus.Blackjack)
                    hand.Result = HandResult.Blackjack;
                else
                    hand.Result = _rules.DetermineResult(hand, table.Dealer.Cards);

                hand.Payout = _rules.CalculatePayout(hand, hand.Result);
                player.Chips += hand.Payout;
            }

            player.HasPlacedBet = false;
            player.CurrentBet = 0;

            if (player.Chips <= 0)
            {
                player.Chips = 0;
                player.IsBankrupt = true;
                player.LastActionMessage = "Банкрот!";
            }
        }

        table.Phase = GamePhase.RoundComplete;
        table.ActivePlayerId = null;
        table.LastEventMessage = "Раунд завершён. Сделайте ставки для нового раунда.";
    }

    private void PrepareNextBettingRound(TableState table)
    {
        foreach (var player in table.Players.Where(p => p.IsConnected))
        {
            if (!player.IsBankrupt)
            {
                player.HasPlacedBet = false;
                player.CurrentBet = 0;
                player.Hands.Clear();
                player.CurrentHandIndex = 0;
            }
        }

        table.Dealer = new Dealer();
        table.ActivePlayerId = null;
        table.Phase = GamePhase.Betting;
        table.LastEventMessage = "Сделайте ставки.";
    }

    private bool HasActiveHand(Player player)
    {
        for (var i = player.CurrentHandIndex; i < player.Hands.Count; i++)
        {
            var hand = player.Hands[i];
            if (hand.Status == HandStatus.Active)
            {
                player.CurrentHandIndex = i;
                return true;
            }
        }
        return false;
    }

    private static int FindNextActiveHandIndex(Player player)
    {
        for (var i = 0; i < player.Hands.Count; i++)
        {
            if (player.Hands[i].Status == HandStatus.Active)
                return i;
        }
        return -1;
    }

    private PlayerHand? GetActiveHand(Player player)
    {
        if (player.CurrentHandIndex < 0 || player.CurrentHandIndex >= player.Hands.Count)
            return null;

        var hand = player.Hands[player.CurrentHandIndex];
        return hand.Status == HandStatus.Active ? hand : null;
    }

    private Card Draw(TableState table) => _rules.DrawCard(table.Deck);

    private TableState? FindTableByConnection(string connectionId) =>
        _tables.Values.FirstOrDefault(t => t.Players.Any(p => p.ConnectionId == connectionId && p.IsConnected));

    private static int FindFreeSeat(TableState table)
    {
        var taken = table.Players.Select(p => p.SeatIndex).ToHashSet();
        for (var i = 0; i < TableState.MaxPlayers; i++)
        {
            if (!taken.Contains(i))
                return i;
        }
        return table.Players.Count;
    }

    private static string SanitizeName(string? name, string fallback = "Player") =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim()[..Math.Min(name.Trim().Length, 20)];
}