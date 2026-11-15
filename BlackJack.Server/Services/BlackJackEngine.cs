using BlackJack.Shared;

namespace BlackJack.Server.Services;

public class BlackJackEngine
{
    private List<Card> _desk = new();

    public void InitialDesk() 
    {
        _desk.Clear();
        var suits = Enum.GetValues<Suit>();
        var ranks = Enum.GetValues<Rank>();

        var cards = suits.SelectMany(suit => ranks, (suit, rank) => new Card
        {
            Suit = suit,
            Rank = rank
        }).ToArray();

        Random.Shared.Shuffle(cards);
        _desk = cards.ToList();
    }
}
