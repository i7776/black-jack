using System;
using System.Collections.Generic;
using System.Text;

namespace BlackJack.Shared;

public class Player
{
    public string Name { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public List<Card> Cards { get; set; } = new();
    public int Score { get; set; } = 0;
    public PlayerStatus Status { get; set; } = PlayerStatus.Waiting;
}

public enum PlayerStatus //пока не понимаю, какие нужны
{
    Waiting,
    Playing,
    Bust,
    Stand,
    BlackJack,
    Lost,
    Draw,
    Won
}