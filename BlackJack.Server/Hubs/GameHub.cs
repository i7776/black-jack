using BlackJack.Shared;
using Microsoft.AspNetCore.SignalR;

namespace BlackJack.Server.Hubs;

public class GameHub : Hub<IGameClient>
{
    private static Dictionary<string, GameState> _games = new();
    public async Task JoinRoom(string roomId, string playerName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        var state = 
        await Clients.Group(roomId).UpdateGameState(state);
    }
    public async Task Hit()
    {
        await Clients.Group(roomId).UpdateGameState(state);
    }
    public async Task Stand()
    {
        await Clients.Group(roomId).UpdateGameState(state);
    }
}
