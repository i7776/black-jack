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

            if (maxPlayers < 2 || maxPlayers > 5)
            {
                throw new ArgumentException("В комнате может быть от 2 до 5 игроков.");
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
            {
                throw new ArgumentException("Укажи имя игрока.");
            }

            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                if (room.State.Players.Any(p => p.PlayerId == playerId))
                {
                    return CreateUpdate(room, playerId);
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

                return CreateUpdate(room, playerId);
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

        private static RoomUpdate CreateUpdate(GameRoom room, string targetPlayerId)
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

                    DealerCards = room.State.DealerCards.Select(CopyCard).ToList(),

                    Players = room.State.Players.Select(player => new Player
                    {
                        PlayerId = player.PlayerId,
                        Name = player.Name,

                        Score = (player.PlayerId == targetPlayerId || room.State.GameStatus == GameStatus.Finished)
                            ? player.Score
                            : 0,

                        Status = player.Status,

                        Cards = player.PlayerId == targetPlayerId 
                            ? player.Cards.Select(CopyCard).ToList() 
                            : player.Cards.Select(c => new Card { Suit = default, Rank = default }).ToList()

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

                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate Stand(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Stand(room.State, playerId);

                return CreateUpdate(room, playerId);
            }
        }

        public RoomUpdate Surrender(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                room.Engine.Surrender(room.State, playerId);
                return CreateUpdate(room, playerId);
            }
        }
        public RoomUpdate LeaveRoom(string roomId, string playerId)
        {
            var room = GetRoom(roomId);

            lock (room.SyncRoot)
            {
                var player = room.State.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (player != null)
                {
                    room.State.Players.Remove(player);
                }
               
                if (room.State.Players.Count == 0)
                {
                    _rooms.TryRemove(roomId, out _);
                }

                return CreateUpdate(room, playerId);
            }
        }

        public Dictionary<string, GameState> GetRoomUpdatesForEveryone(string roomId)
        {
            var room = GetRoom(roomId);
            var dict = new Dictionary<string, GameState>();

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
    }
}
