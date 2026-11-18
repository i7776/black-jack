using BlackJack.Shared;

namespace BlackJack.Server.Services;

public class RoomUpdate
{
    public required string RoomId { get; init; }

    public required GameState State { get; init; }
}