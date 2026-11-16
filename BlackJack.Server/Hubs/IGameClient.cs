using BlackJack.Shared;

namespace BlackJack.Server.Hubs
{
    public interface IGameClient
    {
        Task UpdateGameState(GameState state);
        Task ReceiveMessage(string message);
    }
}
