using BlackJack.Server.Services;
using BlackJack.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BlackJack.Server.Hubs;

public class GameHub : Hub<IGameClient>
{
    private static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return RandomNumberGenerator.GetString(chars, 6);
    }
    public async Task CreateRoom(string playerName)
    {
        string roomId;
        do
        {
            roomId = GenerateRoomCode();
        } while (_rooms.ContainsKey(roomId));

        var new_room = new GameRoom()
        {
            RoomId = roomId,
            State = new GameState()
        };

        _rooms[roomId] = new_room;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var player = new Player()
        {
            PlayerId = Context.ConnectionId,
            Name = playerName,
        };
        new_room.State.Players.Add(player);

        await Clients.Caller.ReceiveMessage($"Room with this code {roomId} is created");
    }
    public async Task JoinRoom(string roomId, string playerName)
    {
        roomId = roomId.Trim().ToUpper();
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.ReceiveMessage($"Room with this code {roomId} is not found...");
            return;
        }

        if (room.State.Players.Count == room.State.MaxPlayers && room.State.GameStatus != GameStatus.WaitingForPlayers)
        {
            await Clients.Caller.ReceiveMessage("Game is started");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var new_player = new Player()
        {
            PlayerId = Context.ConnectionId,
            Name = playerName
        };

        room.State.Players.Add(new_player);

        if (room.State.Players.Count == room.State.MaxPlayers)
        {
            room.Engine.StartNewGame(room.State);
        }
        else
        {
            await Clients.Caller.ReceiveMessage("Waiting for other players");
            return;
        }
        await Clients.Group(roomId).UpdateGameState(room.State);
    }
    public async Task Hit()
    {
    }
    public async Task Stand()
    {
    }
}
