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
    public async Task CreateRoom(string playerName, int max_players)
    {
        string roomId;
        do
        {
            roomId = GenerateRoomCode();
        } while (_rooms.ContainsKey(roomId));

        var new_room = new GameRoom()
        {
            RoomId = roomId,
            State = new GameState
            {
                MaxPlayers = max_players
            }
        };

        _rooms[roomId] = new_room;

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var player = new Player
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

        if (room.State.Players.Any(p => p.PlayerId == Context.ConnectionId)) return;

        if (room.State.Players.Count >= room.State.MaxPlayers)
        {
            await Clients.Caller.ReceiveMessage("Game is started");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var new_player = new Player
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
            await Clients.Caller.ReceiveMessage($"Waiting for other players. Now we have {room.State.Players.Count}/{room.State.MaxPlayers}");
        }
        await Clients.Group(roomId).UpdateGameState(room.State);
    }
    public async Task Hit(string roomId)
    {
        var room = _rooms[roomId];
        var player = room.State.Players.FirstOrDefault(p => p.PlayerId == Context.ConnectionId);

        if (player == null || player.Status != PlayerStatus.PlayerTurns || room.State.GameStatus != GameStatus.PlayersTurns) return; //я не понимаю как огранизовать с этими статусами

        player.Cards.Add(room.Engine.DealCard());
        player.Score = room.Engine.CalculateScore(player.Cards);

        if (player.Score > 21)
        {
            player.Status = PlayerStatus.Bust;
            await MoveToNextPlayer(room);
        }
        await Clients.Group(roomId).UpdateGameState(room.State);
    }

    private async Task MoveToNextPlayer(GameRoom room)
    {
        room.State.CurrentPlayerIndex++;

        if (room.State.CurrentPlayerIndex >= room.State.Players.Count)
        {
            room.State.GameStatus = GameStatus.DealerTurns;
            room.Engine.DealerTurn(room.State); 
        }
        else
        {
            var nextPlayer = room.State.Players[room.State.CurrentPlayerIndex];
        }
    }
    public async Task Stand(string roomId)
    {
        var room = _rooms[roomId];
        if (room.State.Players[room.State.CurrentPlayerIndex].PlayerId != Context.ConnectionId) return;

        room.State.Players[room.State.CurrentPlayerIndex].Status = PlayerStatus.Stand;

        await MoveToNextPlayer(room);
        await Clients.Group(roomId).UpdateGameState(room.State);
    }
}
