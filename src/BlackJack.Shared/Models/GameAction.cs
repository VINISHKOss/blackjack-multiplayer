namespace BlackJack.Shared.Models;

public enum GameAction
{
    Hit,
    Stand,
    Double,
    Split
}

public enum GamePhase
{
    WaitingForPlayers,
    Betting,
    Dealing,
    PlayerTurn,
    DealerTurn,
    Settlement,
    RoundComplete
}

public enum HandStatus
{
    Active,
    Standing,
    Busted,
    Doubled,
    Blackjack,
    Surrendered
}

public enum HandResult
{
    None,
    Win,
    Lose,
    Push,
    Blackjack
}