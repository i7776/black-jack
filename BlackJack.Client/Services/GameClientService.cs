using BlackJack.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlackJack.Client.Services;

public class GameClientService : IAsyncDisposable
{
    private readonly HubConnection _connection; // подключение  к серверу

    public string? RoomId { get; private set; }
    public GameState? State { get; private set; }
    public string? Message { get; private set; }
    public string? Warning { get; private set; }
    public bool IsConnected => _connection.State == HubConnectionState.Connected; // установлено ли соединение
    public event Action? Changed; // для уведомления интерфейса об изменениях


    public string? ConnectionId => _connection.ConnectionId;
    public bool IsHost => State?.HostPlayerId == ConnectionId;
    public bool CanStartGame =>
        IsHost &&
        State != null &&
        State.Players.Count >= 2 &&
        State.GameStatus == GameStatus.WaitingForPlayers;


    public GameClientService()
    {
        // создаём подключение но не запускаем его
        _connection = new HubConnectionBuilder()
            .WithUrl("https://localhost:7281/gamehub")
            .Build();

        _connection.On<GameState>("UpdateGameState", state =>
        {
            State = state;
            Changed?.Invoke();
        });

        _connection.On<string>("ReceiveMessage", message =>
        {
            Message = message;
            Changed?.Invoke();
        });

        _connection.On<string>("ShowWarning", message =>
        {
            Warning = message;
            Changed?.Invoke();
        });

        _connection.Closed += exception =>
        {
            State = null;
            RoomId = null;
            Warning = "Соединение с сервером закрыто.";

            Changed?.Invoke();

            return Task.CompletedTask;
        };
    }

    public async Task StartGameAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(RoomId)) return;
        await _connection.InvokeAsync("StartGame", RoomId);
    }
    public async Task ConnectAsync()
    {
        // подключение из отключённого состояния
        if (_connection.State != HubConnectionState.Disconnected)
        {
            return;
        }

        Warning = null;

        await _connection.StartAsync();

        Changed?.Invoke();
    }
    public async Task CreateRoomAsync(string playerName, int maxPlayers)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException(
                "Сначала подключись к серверу.");
        }

        Warning = null;
        Message = null;

        // получаем результат серверного метода
        var createdRoomId = await _connection.InvokeAsync<string?>(
            "CreateRoom",
            playerName,
            maxPlayers);

        if (!string.IsNullOrWhiteSpace(createdRoomId))
        {
            RoomId = createdRoomId;
        }

        Changed?.Invoke();
    }

    public async Task JoinRoomAsync(string roomId, string playerName)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException(
                "Сначала подключись к серверу.");
        }

        Warning = null;
        Message = null;

        // сохраняем код только после успешного входа
        var joinedRoomId = await _connection.InvokeAsync<string?>(
            "JoinRoom",
            roomId,
            playerName);

        if (!string.IsNullOrWhiteSpace(joinedRoomId))
        {
            RoomId = joinedRoomId;
        }

        Changed?.Invoke();
    }
    public async Task LeaveRoomAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(RoomId))
            return;

        await _connection.InvokeAsync("LeaveRoom", RoomId);
        RoomId = null;
        State = null;
        Changed?.Invoke();
    }

    public async Task PrepareNextRoundAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(RoomId)) return;
        Warning = null;
        await _connection.InvokeAsync("PrepareNextRound", RoomId);
    }

    public async Task SurrenderAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(RoomId))
            throw new InvalidOperationException("Нет подключения к комнате.");

        await _connection.InvokeAsync("Surrender", RoomId);
    }
    public bool IsMyTurn
    {
        get
        {
            var state = State;

            if (!IsConnected ||
                string.IsNullOrWhiteSpace(RoomId) ||
                state is null ||
                state.GameStatus != GameStatus.PlayersTurns)
            {
                return false;
            }

            var index = state.CurrentPlayerIndex;

            if (index < 0 || index >= state.Players.Count)
            {
                return false;
            }

            var player = state.Players[index];

            // сервер использует идентификатор соединения как playerId
            return player.PlayerId == _connection.ConnectionId &&
                   player.Status == PlayerStatus.Playing;
        }
    }

    public async Task HitAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(RoomId))
        {
            throw new InvalidOperationException(
                "Нет подключения к комнате.");
        }

        Warning = null;

        // просим сервер выдать карту
        await _connection.InvokeAsync("Hit", RoomId);
    }

    public async Task StandAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(RoomId))
        {
            throw new InvalidOperationException(
                "Нет подключения к комнате.");
        }

        Warning = null;

        // просим сервер завершить наш ход
        await _connection.InvokeAsync("Stand", RoomId);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync(); // освобождаем соединение
    }
}