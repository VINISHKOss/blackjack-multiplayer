namespace BlackJack.Shared.Models;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum Rank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public sealed class Card
{
    public Suit Suit { get; init; }
    public Rank Rank { get; init; }
    public bool IsFaceDown { get; set; }

    public int BaseValue => Rank switch
    {
        Rank.Ace => 11,
        Rank.King or Rank.Queen or Rank.Jack => 10,
        _ => (int)Rank
    };

    public string DisplayName => Rank switch
    {
        Rank.Ace => "A",
        Rank.King => "K",
        Rank.Queen => "Q",
        Rank.Jack => "J",
        _ => ((int)Rank).ToString()
    };

    public string SuitSymbol => Suit switch
    {
        Suit.Hearts => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs => "♣",
        Suit.Spades => "♠",
        _ => "?"
    };

    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;
}