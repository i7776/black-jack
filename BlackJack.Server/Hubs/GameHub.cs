using BlackJack.Server.Services;
using BlackJack.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BlackJack.Server.Hubs;

public class GameHub : Hub<IGameClient>
{

    private readonly RoomService _rooms;

    public GameHub(RoomService rooms)
    {
        _rooms = rooms;
    }

    public async Task<string?> CreateRoom(string playerName, int maxPlayers)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.CreateRoom(Context.ConnectionId, playerName, maxPlayers);
        }
        catch (ArgumentException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return null;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, update.RoomId);

        await Clients.Caller.ReceiveMessage($"Комната создана. Код: {update.RoomId}");

        await BroadcastStateToRoom(update.RoomId);

        return update.RoomId;
    }

    public async Task<string?> JoinRoom(string roomId, string playerName)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.JoinRoom(
                roomId,
                Context.ConnectionId,
                playerName);
        }
        catch (ArgumentException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return null;
        }
        catch (InvalidOperationException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return null;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            update.RoomId);

        if (update.State.GameStatus ==
            BlackJack.Shared.GameStatus.WaitingForPlayers)
        {
            await Clients.Caller.ReceiveMessage(
                $"Ожидаем игроков: " +
                $"{update.State.Players.Count}/" +
                $"{update.State.MaxPlayers}");
        }

        await BroadcastStateToRoom(update.RoomId);

        return update.RoomId;
    }

    public async Task Hit(string roomId)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.Hit(roomId, Context.ConnectionId);
            await BroadcastStateToRoom(update.RoomId);
        }
        catch (InvalidOperationException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return;
        }
        catch (ArgumentException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return;
        }
    }

    public async Task Stand(string roomId)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.Stand(roomId, Context.ConnectionId);
            await BroadcastStateToRoom(update.RoomId);
        }
        catch (InvalidOperationException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return;
        }
        catch (ArgumentException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return;
        }
    }

    public async Task Surrender(string roomId)
    {
        try
        {
            _rooms.Surrender(roomId, Context.ConnectionId);
            await BroadcastStateToRoom(roomId);
        }
        catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
        {
            await Clients.Caller.ShowWarning(exception.Message);
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        _rooms.LeaveRoom(roomId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await BroadcastStateToRoom(roomId);
    }

    private async Task BroadcastStateToRoom(string roomId)
    {
        var updates = _rooms.GetRoomUpdatesForEveryone(roomId);

        foreach (var (connectionId, state) in updates)
        {
            await Clients.Client(connectionId).UpdateGameState(state);
        }
    }
}
