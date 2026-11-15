using BlackJack.Shared;

namespace BlackJack.Server.Services;

public class BlackJackEngine
{
    private List<Card> _desk = new();

    public void InitialDesk() //нам нужно создать перемешанную колоду хммммммм
    {
        _desk.Clear();
        string[] suits = { "Hearts", "Diamonds", "Spades", "Clubs" };
        string[] ranks = { "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };

        _desk = suits.SelectMany(suit => ranks, (suit, rank) => new Card
        {
            Suit = suit,
            Rank = rank,
            Value = GetValueByRank(rank)
        }).OrderBy(_ => Guid.NewGuid()).ToList();
    }
}
