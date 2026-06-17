using BlackJack.Shared.Models;

namespace BlackJack.Server.Services;

public sealed class TableStateMapper
{
    private readonly BlackjackRulesService _rules;

    public TableStateMapper(BlackjackRulesService rules)
    {
        _rules = rules;
    }

    public TableStateDto ToDto(TableState table, bool revealDealerHole = false)
    {
        var hideDealerHole = !revealDealerHole &&
            table.Phase is GamePhase.PlayerTurn or GamePhase.Betting or GamePhase.Dealing or GamePhase.WaitingForPlayers;

        return new TableStateDto
        {
            Id = table.Id,
            Name = table.Name,
            Phase = table.Phase,
            ActivePlayerId = table.ActivePlayerId,
            LastEventMessage = table.LastEventMessage,
            RoundNumber = table.RoundNumber,
            Players = table.Players.Select(p => ToPlayerDto(p, table.Phase)).ToList(),
            Dealer = ToDealerDto(table.Dealer, hideDealerHole)
        };
    }

    private PlayerDto ToPlayerDto(Player player, GamePhase phase)
    {
        return new PlayerDto
        {
            Id = player.Id,
            Name = player.Name,
            SeatIndex = player.SeatIndex,
            Chips = player.Chips,
            CurrentBet = player.CurrentBet,
            HasPlacedBet = player.HasPlacedBet,
            IsBankrupt = player.IsBankrupt,
            IsConnected = player.IsConnected,
            LastActionMessage = player.LastActionMessage,
            CurrentHandIndex = player.CurrentHandIndex,
            Hands = player.Hands.Select(h => ToHandDto(h, phase)).ToList()
        };
    }

    private HandDto ToHandDto(PlayerHand hand, GamePhase phase)
    {
        var (value, isSoft) = _rules.CalculateHandValue(hand.Cards);
        var showResults = phase == GamePhase.RoundComplete;
        return new HandDto
        {
            Cards = hand.Cards.Select(ToCardDto).ToList(),
            Bet = hand.Bet,
            Status = hand.Status,
            Result = showResults ? hand.Result : HandResult.None,
            HandValue = value,
            IsSoft = isSoft,
            IsSplitHand = hand.IsSplitHand
        };
    }

    private DealerDto ToDealerDto(Dealer dealer, bool hideHoleCard)
    {
        var cards = dealer.Cards.Select((c, i) =>
        {
            if (hideHoleCard && i == 1)
            {
                return new Card
                {
                    Suit = c.Suit,
                    Rank = c.Rank,
                    IsFaceDown = true
                };
            }
            return c;
        }).ToList();

        var (value, isSoft) = _rules.CalculateHandValue(cards, countFaceDown: !hideHoleCard);

        return new DealerDto
        {
            Cards = cards.Select(ToCardDto).ToList(),
            HandValue = hideHoleCard && cards.Count >= 2
                ? _rules.CalculateHandValue(cards.Where(c => !c.IsFaceDown).ToList()).Value
                : value,
            IsSoft = isSoft,
            Status = dealer.Status
        };
    }

    private static CardDto ToCardDto(Card card) => new()
    {
        Suit = card.Suit,
        Rank = card.Rank,
        IsFaceDown = card.IsFaceDown,
        DisplayName = card.IsFaceDown ? "?" : card.DisplayName,
        SuitSymbol = card.IsFaceDown ? "" : card.SuitSymbol,
        IsRed = card.IsRed
    };

    public TableSummary ToSummary(TableState table) => new()
    {
        Id = table.Id,
        Name = table.Name,
        PlayerCount = table.Players.Count(p => p.IsConnected),
        Phase = table.Phase
    };
}