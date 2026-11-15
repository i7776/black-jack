namespace BlackJack.Shared;

public enum Suit
{
    Spades, // пики
    Hearts, // черви
    Diamonds, // бубны
    Clubs // трефы

}; //масть

public enum Rank
{
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

public class Card
{
    public Suit Suit { get; set; } // масть
    public Rank Rank { get; set; } // здесь нужно подумать, 6-10 Jack Queen King Ace 
    public int Value  => Rank switch
    {
        Rank.Jack or Rank.Queen or Rank.King => 10,
        Rank.Ace => 11,
        _ => (int)Rank
    };

}

