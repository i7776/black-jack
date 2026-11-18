using BlackJack.Shared;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BlackJack.Server.Services
{
    public class RoomService
    {
        private static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

        public static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return RandomNumberGenerator.GetString(chars, 6);
        }

        public RoomUpdate CreateRoom( string playerId, string playerName, int maxPlayers)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentException("Укажи имя игрока.");
            }

            if (maxPlayers < 2)
            {
                throw new ArgumentException(
                    "В комнате должно быть минимум два игрока.");
            }

            while (true)
            {
                var roomId = GenerateRoomCode();

                var room = new GameRoom
                {
                    RoomId = roomId,
                    State = new GameState
                    {
                        MaxPlayers = maxPlayers
                    }
                };

                room.State.Players.Add(new Player
                {
                    PlayerId = playerId,
                    Name = playerName.Trim(),
                    Status = PlayerStatus.Waiting
                });

                var update = CreateUpdate(room);

                if (_rooms.TryAdd(roomId, room))
                {
                    return update;
                }

            }
        }
        public RoomUpdate JoinRoom(string roomId, string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentException("Укажи имя игрока.");
            }

            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                // Повторное присоединение не создаёт второго игрока.
                if (room.State.Players.Any(p => p.PlayerId == playerId))
                {
                    return CreateUpdate(room);
                }

                if (room.State.GameStatus != GameStatus.WaitingForPlayers)
                {
                    throw new InvalidOperationException("Игра в этой комнате уже началась.");
                }

                if (room.State.Players.Count >= room.State.MaxPlayers)
                {
                    throw new InvalidOperationException("В комнате нет свободных мест.");
                }

                room.State.Players.Add(new Player
                {
                    PlayerId = playerId,
                    Name = playerName.Trim(),
                    Status = PlayerStatus.Waiting
                });

                if (room.State.Players.Count == room.State.MaxPlayers)
                {
                    room.Engine.StartNewGame(room.State);
                }

                return CreateUpdate(room);
            }
        }

        private GameRoom GetRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Укажи код комнаты.");
            }

            roomId = roomId.Trim().ToUpperInvariant();

            if (!_rooms.TryGetValue(roomId, out var room))
            {
                throw new InvalidOperationException("Комната с таким кодом не найдена.");
            }

            return room;
        }

        private static RoomUpdate CreateUpdate(GameRoom room)
        {
            return new RoomUpdate
            {
                RoomId = room.RoomId,
                State = new GameState
                {
                    MaxPlayers = room.State.MaxPlayers,
                    CurrentPlayerIndex = room.State.CurrentPlayerIndex,
                    GameStatus = room.State.GameStatus,
                    DealerScore = room.State.DealerScore,

                    DealerCards = room.State.DealerCards
                        .Select(CopyCard)
                        .ToList(),

                    Players = room.State.Players.Select(player => new Player
                    {
                        PlayerId = player.PlayerId,
                        Name = player.Name,
                        Score = player.Score,
                        Status = player.Status,
                        Cards = player.Cards.Select(CopyCard).ToList()
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

        public RoomUpdate Hit(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Hit(room.State, playerId);

                return CreateUpdate(room);
            }
        }

        public RoomUpdate Stand(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Stand(room.State, playerId);

                return CreateUpdate(room);
            }
        }
    }
}
