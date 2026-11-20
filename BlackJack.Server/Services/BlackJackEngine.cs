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

    public Card DealCard()
    {
        if (_desk.Count == 0)
        {
            InitialDesk();
        }
        var card = _desk.First();
        _desk.Remove(card);
        return card;
    }

    public void StartNewGame(GameState gameState)
    {
        InitialDesk();

        gameState.DealerCards.Clear();
        gameState.DealerCards.Add(DealCard());
        gameState.DealerScore = CalculateScore(gameState.DealerCards);

        gameState.CurrentPlayerIndex = 0;

        foreach (var player in gameState.Players)
        {
            player.Cards.Clear();
            player.Score = 0; 

            player.Cards.Add(DealCard());
            player.Cards.Add(DealCard());
            player.Score = CalculateScore(player.Cards);
            player.Status = PlayerStatus.Playing;
        }

        gameState.GameStatus = GameStatus.PlayersTurns;
    }

    public void DealerTurn(GameState state)
    {
        state.GameStatus = GameStatus.DealerTurns;
        state.DealerScore = CalculateScore(state.DealerCards);

        while (state.DealerScore < 17)
        {
            state.DealerCards.Add(DealCard());
            state.DealerScore = CalculateScore(state.DealerCards);
        }

        DetermineWinners(state);
        state.GameStatus = GameStatus.Finished;
    }

    public void DetermineWinners(GameState state)
    {
        int dealerScore = state.DealerScore;

        foreach (var player in state.Players)
        {
            player.Score = CalculateScore(player.Cards);

            // Если игрок сдался — он остается сдавшимся (проиграл досрочно)
            if (player.Status == PlayerStatus.Surrender)
            {
                continue;
            }

            // Если перебор — статус Bust
            if (player.Score > 21)
            {
                player.Status = PlayerStatus.Bust;
            }
            // Натуральный Блэкджек (21 с первых 2 карт)
            else if (player.Score == 21 && player.Cards.Count == 2 && !(dealerScore == 21 && state.DealerCards.Count == 2))
            {
                player.Status = PlayerStatus.BlackJack;
            }
            else if (dealerScore > 21 || player.Score > dealerScore)
            {
                player.Status = PlayerStatus.Won;
            }
            else if (player.Score < dealerScore)
            {
                player.Status = PlayerStatus.Lost;
            }
            else
            {
                player.Status = PlayerStatus.Draw;
            }
        }

        state.Message = dealerScore > 21
            ? $"У дилера перебор ({dealerScore} очк.)!"
            : $"Дилер остановился на {dealerScore} очк.";
    }

    private void MoveToNextPlayer(GameState state)
    {
        state.CurrentPlayerIndex++;

        while (state.CurrentPlayerIndex < state.Players.Count)
        {
            var player = state.Players[state.CurrentPlayerIndex];

            if (player.Status == PlayerStatus.Playing)
            {
                state.Message = $"Ход перешел к {player.Name}";
                return;
            }

            state.CurrentPlayerIndex++;
        }

        state.GameStatus = GameStatus.DealerTurns;
        DealerTurn(state);
    }

    public void ResetToWaiting(GameState state)
    {
        state.DealerCards.Clear();
        state.DealerScore = 0;
        state.CurrentPlayerIndex = 0;
        state.GameStatus = GameStatus.WaitingForPlayers;

        foreach (var player in state.Players)
        {
            player.Cards.Clear();
            player.Score = 0;
            player.Status = PlayerStatus.Waiting;
        }

        state.Message = state.Players.Count < 2
            ? "Ожидание подключения хотя бы одного соперника..."
            : $"Игроки готовы ({state.Players.Count}/{state.MaxPlayers}). Хост может начать раунд!";
    }

    public void Hit(GameState state, string playerId)
    {
        if (state.GameStatus != GameStatus.PlayersTurns)
        {
            throw new InvalidOperationException(
                "Сейчас нельзя брать карту.");
        }

        if (state.CurrentPlayerIndex < 0 || state.CurrentPlayerIndex >= state.Players.Count)
        {
            throw new InvalidOperationException(
                "Текущий игрок не определён.");
        }

        var player = state.Players[state.CurrentPlayerIndex];

        if (player.PlayerId != playerId)
        {
            throw new InvalidOperationException(
                "Сейчас ход другого игрока.");
        }

        if (player.Status != PlayerStatus.Playing)
        {
            throw new InvalidOperationException(
                "Этот игрок уже завершил ход.");
        }

        player.Cards.Add(DealCard());
        player.Score = CalculateScore(player.Cards);

        if (player.Score > 21)
        {
            player.Status = PlayerStatus.Bust;
            MoveToNextPlayer(state);
        }
    }

    public void Stand(GameState state, string playerId)
    {
        if (state.GameStatus != GameStatus.PlayersTurns)
        {
            throw new InvalidOperationException(
                "Сейчас нельзя завершить ход.");
        }

        if (state.CurrentPlayerIndex < 0 ||
            state.CurrentPlayerIndex >= state.Players.Count)
        {
            throw new InvalidOperationException(
                "Текущий игрок не определён.");
        }

        var player = state.Players[state.CurrentPlayerIndex];

        if (player.PlayerId != playerId)
        {
            throw new InvalidOperationException(
                "Сейчас ход другого игрока.");
        }

        if (player.Status != PlayerStatus.Playing)
        {
            throw new InvalidOperationException(
                "Этот игрок уже завершил ход.");
        }

        player.Status = PlayerStatus.Stand;

        MoveToNextPlayer(state);
    }

    public void Surrender(GameState state, string playerId)
    {
        if (state.GameStatus != GameStatus.PlayersTurns)
            throw new InvalidOperationException("Сейчас нельзя сдаться.");

        var player = state.Players[state.CurrentPlayerIndex];
        if (player.PlayerId != playerId)
            throw new InvalidOperationException("Сейчас ход другого игрока.");

        player.Status = PlayerStatus.Surrender;

        MoveToNextPlayer(state);
    }
}
