using System;
using System.Collections.Generic;
using System.Text;

namespace BlackJack.Shared;

public class GameState
{
    public string RoomId { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = new();
    public List<Card> DealerCards { get; set; } = new();
    public int DealerScore => DealerCards.Sum(x => x.Value);

    public GameStatus GameStatus { get; set; } = GameStatus.WaitingForPlayers;
 
}

public enum GameStatus
{
    WaitingForPlayers,
    DealerTurns,
    PlayerTurns,
    Finished

}