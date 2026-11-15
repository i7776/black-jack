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

    public int CalculateScore(List<Card> cards)
    {
        var scope = cards.Sum(x => x.Value);
        var ace_count = cards.Count(x => x.Rank == Rank.Ace);

        while (ace_count > 0 && scope > 21)
        {
            scope -= 10;
            --ace_count;
        }
        return scope;
    }
}
