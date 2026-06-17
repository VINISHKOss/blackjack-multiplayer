using BlackJack.Shared.Models;

namespace BlackJack.Server.Services;

public sealed class BlackjackRulesService
{
    public (int Value, bool IsSoft) CalculateHandValue(IReadOnlyList<Card> cards, bool countFaceDown = true)
    {
        var visible = countFaceDown
            ? cards
            : cards.Where(c => !c.IsFaceDown).ToList();

        if (visible.Count == 0)
            return (0, false);

        var total = 0;
        var aces = 0;

        foreach (var card in visible)
        {
            if (card.Rank == Rank.Ace)
            {
                aces++;
                total += 11;
            }
            else
            {
                total += card.BaseValue;
            }
        }

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        var isSoft = aces > 0 && total <= 21;
        return (total, isSoft);
    }

    public bool IsBlackjack(IReadOnlyList<Card> cards) =>
        cards.Count == 2 && CalculateHandValue(cards).Value == 21;

    public bool IsBusted(IReadOnlyList<Card> cards) =>
        CalculateHandValue(cards).Value > 21;

    public bool CanSplit(PlayerHand hand) =>
        hand.Cards.Count == 2 &&
        hand.Cards[0].Rank == hand.Cards[1].Rank &&
        !hand.IsSplitHand;

    public bool CanDouble(Player player, PlayerHand hand) =>
        hand.Cards.Count == 2 &&
        hand.Status == HandStatus.Active &&
        player.Chips >= hand.Bet;

    public bool DealerShouldHit(IReadOnlyList<Card> cards)
    {
        var (value, isSoft) = CalculateHandValue(cards);
        if (value < 17)
            return true;

        // Stand on soft 17 (S17 rule)
        return false;
    }

    public HandResult DetermineResult(PlayerHand playerHand, IReadOnlyList<Card> dealerCards)
    {
        var playerValue = CalculateHandValue(playerHand.Cards).Value;
        var dealerValue = CalculateHandValue(dealerCards).Value;
        var dealerBusted = dealerValue > 21;
        var playerBusted = playerValue > 21;

        if (playerHand.Status == HandStatus.Busted || playerBusted)
            return HandResult.Lose;

        if (IsBlackjack(playerHand.Cards) && !IsBlackjack(dealerCards))
            return HandResult.Blackjack;

        if (dealerBusted)
            return HandResult.Win;

        if (playerValue > dealerValue)
            return HandResult.Win;

        if (playerValue < dealerValue)
            return HandResult.Lose;

        return HandResult.Push;
    }

    public int CalculatePayout(PlayerHand hand, HandResult result)
    {
        return result switch
        {
            HandResult.Blackjack => hand.Bet + (int)(hand.Bet * 1.5m),
            HandResult.Win => hand.Bet * 2,
            HandResult.Push => hand.Bet,
            _ => 0
        };
    }

    public List<Card> CreateShoe(int deckCount = 6)
    {
        var deck = new List<Card>();
        for (var d = 0; d < deckCount; d++)
        {
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    deck.Add(new Card { Suit = suit, Rank = rank });
                }
            }
        }

        Shuffle(deck);
        return deck;
    }

    public void Shuffle(List<Card> deck)
    {
        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    public Card DrawCard(List<Card> deck)
    {
        if (deck.Count == 0)
        {
            var fresh = CreateShoe();
            deck.AddRange(fresh);
        }

        var card = deck[^1];
        deck.RemoveAt(deck.Count - 1);
        card.IsFaceDown = false;
        return card;
    }
}