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

    public async Task CreateRoom(string playerName, int maxPlayers)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.CreateRoom( Context.ConnectionId, playerName, maxPlayers);
        }
        catch (ArgumentException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, update.RoomId);

        await Clients.Caller.ReceiveMessage(
            $"Комната создана. Код: {update.RoomId}");

        await Clients.Group(update.RoomId)
            .UpdateGameState(update.State);
    }

    public async Task JoinRoom(string roomId, string playerName)
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
            return;
        }
        catch (InvalidOperationException exception)
        {
            await Clients.Caller.ShowWarning(exception.Message);
            return;
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

        await Clients.Group(update.RoomId)
            .UpdateGameState(update.State);
    }

    public async Task Hit(string roomId)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.Hit(roomId, Context.ConnectionId);
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

        await Clients.Group(update.RoomId)
            .UpdateGameState(update.State);
    }

    public async Task Stand(string roomId)
    {
        RoomUpdate update;

        try
        {
            update = _rooms.Stand(roomId, Context.ConnectionId);
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

        await Clients.Group(update.RoomId)
            .UpdateGameState(update.State);
    }
}
