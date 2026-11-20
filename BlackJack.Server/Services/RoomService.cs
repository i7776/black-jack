using BlackJack.Server.Hubs;
using BlackJack.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BlackJack.Server.Services
{
    public class RoomService
    {
        private static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _turnTimers = new();

        private readonly IHubContext<GameHub, IGameClient> _hubContext;

        public RoomService(IHubContext<GameHub, IGameClient> hubContext)
        {
            _hubContext = hubContext;
        }

        public static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return RandomNumberGenerator.GetString(chars, 6);
        }

        public RoomUpdate CreateRoom(string playerId, string playerName, int maxPlayers)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                throw new ArgumentException("Укажи имя игрока.");

            if (maxPlayers < 2 || maxPlayers > 5)
                throw new ArgumentException("В комнате может быть от 2 до 5 игроков.");

            while (true)
            {
                var roomId = GenerateRoomCode();

                var room = new GameRoom
                {
                    RoomId = roomId,
                    State = new GameState
                    {
                        HostPlayerId = playerId,
                        MaxPlayers = maxPlayers,
                        Message = $"Стол {roomId} создан. Ожидание игроков..."
                    }
                };

                room.State.Players.Add(new Player
                {
                    PlayerId = playerId,
                    Name = playerName.Trim(),
                    Status = PlayerStatus.Waiting,
                    IsHost = true
                });

                var update = CreateUpdate(room, playerId);

                if (_rooms.TryAdd(roomId, room))
                {
                    return update;
                }
            }
        }

        public RoomUpdate JoinRoom(string roomId, string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                throw new ArgumentException("Укажи имя игрока.");

            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                if (room.State.Players.Any(p => p.PlayerId == playerId))
                    return CreateUpdate(room, playerId);

                if (room.State.GameStatus != GameStatus.WaitingForPlayers)
                    throw new InvalidOperationException("Игра в этой комнате уже началась или ещё не сброшена хостом.");

                if (room.State.Players.Count >= room.State.MaxPlayers)
                    throw new InvalidOperationException("В комнате нет свободных мест.");

                room.State.Players.Add(new Player
                {
                    PlayerId = playerId,
                    Name = playerName.Trim(),
                    Status = PlayerStatus.Waiting,
                    IsHost = false
                });

                room.State.Message = $"Игрок {playerName} сел за стол ({room.State.Players.Count}/{room.State.MaxPlayers}).";

                if (room.State.Players.Count == room.State.MaxPlayers)
                {
                    room.Engine.StartNewGame(room.State);
                    StartTurnTimer(room.RoomId); // Запускаем таймер 1-го игрока!
                }

                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate StartGame(string roomId, string playerId)
        {
            var room = GetRoom(roomId);
            lock (room.SyncRoot)
            {
                if (room.State.HostPlayerId != playerId)
                    throw new InvalidOperationException("Только создатель комнаты может начать игру.");

                if (room.State.Players.Count < 2)
                    throw new InvalidOperationException("Для игры нужно минимум 2 игрока.");

                room.Engine.StartNewGame(room.State);
                StartTurnTimer(room.RoomId); // Запускаем таймер!

                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate ResetToWaiting(string roomId, string playerId)
        {
            var room = GetRoom(roomId);
            lock (room.SyncRoot)
            {
                if (room.State.HostPlayerId != playerId)
                    throw new InvalidOperationException("Только создатель стола может продолжить игру.");

                CancelTurnTimer(roomId);
                room.Engine.ResetToWaiting(room.State);
                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate Hit(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Hit(room.State, playerId);
                UpdateTimerBasedOnGameStatus(room); // Перезапускаем или отменяем таймер
                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate Stand(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Stand(room.State, playerId);
                UpdateTimerBasedOnGameStatus(room); // Перезапускаем или отменяем таймер
                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate Surrender(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Surrender(room.State, playerId);
                UpdateTimerBasedOnGameStatus(room); // Перезапускаем или отменяем таймер
                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate LeaveRoom(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                var playerIndex = room.State.Players.FindIndex(p => p.PlayerId == playerId);
                if (playerIndex != -1)
                {
                    var player = room.State.Players[playerIndex];
                    room.State.Players.RemoveAt(playerIndex);
                    room.State.Message = $"{player.Name} покинул стол.";

                    // Передаем права хоста, если вышел создатель
                    if (player.PlayerId == room.State.HostPlayerId && room.State.Players.Count > 0)
                    {
                        var newHost = room.State.Players[0];
                        newHost.IsHost = true;
                        room.State.HostPlayerId = newHost.PlayerId;
                        room.State.Message += $" Новый хост: {newHost.Name}.";
                    }

                    // Если шла игра, аккуратно поправляем индекс текущего игрока
                    if (room.State.GameStatus == GameStatus.PlayersTurns && room.State.Players.Count > 0)
                    {
                        if (playerIndex < room.State.CurrentPlayerIndex)
                        {
                            room.State.CurrentPlayerIndex--;
                        }
                        else if (playerIndex == room.State.CurrentPlayerIndex)
                        {
                            // Вышел тот, чей был ход — проверяем, остались ли еще ходящие игроки
                            while (room.State.CurrentPlayerIndex < room.State.Players.Count &&
                                   room.State.Players[room.State.CurrentPlayerIndex].Status != PlayerStatus.Playing)
                            {
                                room.State.CurrentPlayerIndex++;
                            }

                            if (room.State.CurrentPlayerIndex >= room.State.Players.Count)
                            {
                                room.Engine.DealerTurn(room.State);
                            }
                            UpdateTimerBasedOnGameStatus(room);
                        }
                    }
                }

                if (room.State.Players.Count == 0)
                {
                    CancelTurnTimer(roomId);
                    _rooms.TryRemove(roomId, out _);
                }

                return CreateUpdate(room, playerId);
            }
        }

        // --- УПРАВЛЕНИЕ ТАЙМЕРОМ ХОДА ---

        private void UpdateTimerBasedOnGameStatus(GameRoom room)
        {
            if (room.State.GameStatus == GameStatus.PlayersTurns)
            {
                StartTurnTimer(room.RoomId);
            }
            else
            {
                CancelTurnTimer(room.RoomId);
            }
        }

        private void CancelTurnTimer(string roomId)
        {
            if (_turnTimers.TryRemove(roomId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }
        }

        private void StartTurnTimer(string roomId)
        {
            CancelTurnTimer(roomId);

            if (!_rooms.TryGetValue(roomId, out var room)) return;
            if (room.State.GameStatus != GameStatus.PlayersTurns) return;

            var turnIndex = room.State.CurrentPlayerIndex;
            if (turnIndex < 0 || turnIndex >= room.State.Players.Count) return;

            var expectedPlayerId = room.State.Players[turnIndex].PlayerId;
            var newCts = new CancellationTokenSource();
            _turnTimers[roomId] = newCts;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Ждём 15 секунд на ход
                    await Task.Delay(15000, newCts.Token);

                    if (newCts.IsCancellationRequested) return;

                    Dictionary<string, GameState>? updatesToSend = null;

                    if (_rooms.TryGetValue(roomId, out var activeRoom))
                    {
                        lock (activeRoom.SyncRoot)
                        {
                            // Двойная проверка: игра всё ещё идёт, и ход всё ещё у того же игрока
                            if (newCts.IsCancellationRequested ||
                                activeRoom.State.GameStatus != GameStatus.PlayersTurns ||
                                activeRoom.State.CurrentPlayerIndex != turnIndex ||
                                turnIndex >= activeRoom.State.Players.Count ||
                                activeRoom.State.Players[turnIndex].PlayerId != expectedPlayerId)
                            {
                                return;
                            }

                            var timedOutPlayer = activeRoom.State.Players[turnIndex];
                            activeRoom.Engine.Stand(activeRoom.State, expectedPlayerId);
                            activeRoom.State.Message = $"Время вышло! Авто-пас за {timedOutPlayer.Name}.";

                            // Перезапускаем таймер для следующего игрока (если игра продолжается)
                            UpdateTimerBasedOnGameStatus(activeRoom);

                            // Готовим персональные состояния для рассылки всем игрокам!
                            updatesToSend = new Dictionary<string, GameState>();
                            foreach (var p in activeRoom.State.Players)
                            {
                                updatesToSend[p.PlayerId] = CreateUpdate(activeRoom, p.PlayerId).State;
                            }
                        }
                    }

                    // РАССЫЛАЕМ ОБНОВЛЕНИЕ В БРАУЗЕРЫ ЧЕРЕЗ HUB CONTEXT!
                    if (updatesToSend != null)
                    {
                        foreach (var (connId, state) in updatesToSend)
                        {
                            await _hubContext.Clients.Client(connId).UpdateGameState(state);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Игрок успел походить до истечения 15 секунд — всё отлично
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в таймере комнаты {roomId}: {ex.Message}");
                }
            }, newCts.Token);
        }


        private GameRoom GetRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                throw new ArgumentException("Укажи код комнаты.");

            roomId = roomId.Trim().ToUpperInvariant();

            if (!_rooms.TryGetValue(roomId, out var room))
                throw new InvalidOperationException("Комната с таким кодом не найдена.");

            return room;
        }

        private static RoomUpdate CreateUpdate(GameRoom room, string targetPlayerId)
        {
            return new RoomUpdate
            {
                RoomId = room.RoomId,
                State = new GameState
                {
                    HostPlayerId = room.State.HostPlayerId,
                    MaxPlayers = room.State.MaxPlayers,
                    CurrentPlayerIndex = room.State.CurrentPlayerIndex,
                    GameStatus = room.State.GameStatus,
                    DealerScore = room.State.DealerScore,
                    Message = room.State.Message,

                    DealerCards = room.State.DealerCards.Select(CopyCard).ToList(),

                    Players = room.State.Players.Select(player => new Player
                    {
                        PlayerId = player.PlayerId,
                        Name = player.Name,
                        IsHost = player.IsHost,
                        Score = (player.PlayerId == targetPlayerId || room.State.GameStatus == GameStatus.Finished)
                            ? player.Score
                            : 0,
                        Status = player.Status,
                        Cards = (player.PlayerId == targetPlayerId || room.State.GameStatus == GameStatus.Finished)
                            ? player.Cards.Select(CopyCard).ToList()
                            : player.Cards.Select(_ => new Card { Suit = default, Rank = default }).ToList()
                    }).ToList()
                }
            };
        }

        private static Card CopyCard(Card card)
        {
            return new Card
            {
                Suit = card.Suit,
                Rank = card.Rank
            };
        }

        public Dictionary<string, GameState> GetRoomUpdatesForEveryone(string roomId)
        {
            roomId = roomId.Trim().ToUpperInvariant();
            var dict = new Dictionary<string, GameState>();

            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return dict;
            }

            lock (room.SyncRoot)
            {
                foreach (var player in room.State.Players)
                {
                    var update = CreateUpdate(room, player.PlayerId);
                    dict[player.PlayerId] = update.State;
                }
            }
            return dict;
        }

        public string? RemoveDisconnectedPlayer(string playerId)
        {
            foreach (var kvp in _rooms)
            {
                var room = kvp.Value;
                lock (room.SyncRoot)
                {
                    if (room.State.Players.Any(p => p.PlayerId == playerId))
                    {
                        LeaveRoom(room.RoomId, playerId);
                        return room.RoomId;
                    }
                }
            }
            return null;
        }
    }
}