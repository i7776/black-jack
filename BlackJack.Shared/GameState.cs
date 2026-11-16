using System;
using System.Collections.Generic;
using System.Text;

namespace BlackJack.Shared;

public class GameState
{
    public List<Player> Players { get; set; } = new();
    public List<Card> DealerCards { get; set; } = new();
    public int DealerScore { get; set; } = 0;
    public int MaxPlayers { get; set; } = 2;

    public GameStatus GameStatus { get; set; } = GameStatus.WaitingForPlayers;
 
}

public enum GameStatus
{
    WaitingForPlayers,
    DealerTurns,
    PlayerTurns,
    Finished

}