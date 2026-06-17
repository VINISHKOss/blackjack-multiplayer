namespace BlackJack.Shared.Models;

public sealed class TableState
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Blackjack Table";
    public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayers;
    public List<Player> Players { get; set; } = [];
    public Dealer Dealer { get; set; } = new();
    public int CurrentPlayerIndex { get; set; } = -1;
    public string? ActivePlayerId { get; set; }
    public string? LastEventMessage { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public List<Card> Deck { get; set; } = [];
    public int RoundNumber { get; set; }
    public bool DealerAnimationPending { get; set; }

    public const int MaxPlayers = 5;

    public Player? GetPlayer(string playerId) =>
        Players.FirstOrDefault(p => p.Id == playerId);

    public Player? GetPlayerByConnection(string connectionId) =>
        Players.FirstOrDefault(p => p.ConnectionId == connectionId);
}

public sealed class TableSummary
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; } = TableState.MaxPlayers;
    public GamePhase Phase { get; init; }
}

public sealed class TableStateDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public GamePhase Phase { get; init; }
    public List<PlayerDto> Players { get; init; } = [];
    public DealerDto Dealer { get; init; } = new();
    public string? ActivePlayerId { get; init; }
    public string? LastEventMessage { get; init; }
    public int RoundNumber { get; init; }
}

public sealed class PlayerDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SeatIndex { get; init; }
    public int Chips { get; init; }
    public int CurrentBet { get; init; }
    public bool HasPlacedBet { get; init; }
    public bool IsBankrupt { get; init; }
    public bool IsConnected { get; init; }
    public string? LastActionMessage { get; init; }
    public List<HandDto> Hands { get; init; } = [];
    public int CurrentHandIndex { get; init; }
}

public sealed class HandDto
{
    public List<CardDto> Cards { get; init; } = [];
    public int Bet { get; init; }
    public HandStatus Status { get; init; }
    public HandResult Result { get; init; }
    public int HandValue { get; init; }
    public bool IsSoft { get; init; }
    public bool IsSplitHand { get; init; }
}

public sealed class DealerDto
{
    public List<CardDto> Cards { get; init; } = [];
    public int HandValue { get; init; }
    public bool IsSoft { get; init; }
    public HandStatus Status { get; init; }
}

public sealed class CardDto
{
    public Suit Suit { get; init; }
    public Rank Rank { get; init; }
    public bool IsFaceDown { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string SuitSymbol { get; init; } = string.Empty;
    public bool IsRed { get; init; }
}