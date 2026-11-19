using BlackJack.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlackJack.Client.Services;

public class GameClientService : IAsyncDisposable
{
    private readonly HubConnection _connection; // подключение  к серверу

    public GameState? State { get; private set; }
    public string? Message { get; private set; }
    public string? Warning { get; private set; }
    public bool IsConnected => _connection.State == HubConnectionState.Connected; // установлено ли соединение
    public event Action? Changed; // для уведомления интерфейса об изменениях

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
            Warning = "Соединение с сервером закрыто.";
            Changed?.Invoke();

            return Task.CompletedTask;
        };
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
            throw new InvalidOperationException("Сначала подключись к серверу.");
        }

        Warning = null;
        Message = null;

        await _connection.InvokeAsync("CreateRoom", playerName, maxPlayers);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync(); // освобождаем соединение
    }
}