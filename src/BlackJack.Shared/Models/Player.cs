namespace BlackJack.Shared.Models;

public sealed class Player
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = "Player";
    public int SeatIndex { get; set; }
    public int Chips { get; set; }
    public List<PlayerHand> Hands { get; set; } = [];
    public int CurrentHandIndex { get; set; }
    public int CurrentBet { get; set; }
    public bool HasPlacedBet { get; set; }
    public bool IsBankrupt { get; set; }
    public bool IsConnected { get; set; } = true;
    public string? LastActionMessage { get; set; }

    public const int StartingChips = 1000;
}

public sealed class PlayerHand
{
    public List<Card> Cards { get; set; } = [];
    public int Bet { get; set; }
    public HandStatus Status { get; set; } = HandStatus.Active;
    public HandResult Result { get; set; } = HandResult.None;
    public int Payout { get; set; }
    public bool IsSplitHand { get; set; }
}

public sealed class Dealer
{
    public List<Card> Cards { get; set; } = [];
    public HandStatus Status { get; set; } = HandStatus.Active;
}