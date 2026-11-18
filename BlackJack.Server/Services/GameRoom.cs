using BlackJack.Shared;

namespace BlackJack.Server.Services
{
    public class GameRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public BlackJackEngine Engine { get; set; } = new();
        public GameState State { get; set; } = new();
        internal object SyncRoot { get; } = new();

    }
}
