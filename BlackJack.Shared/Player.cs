using System;
using System.Collections.Generic;
using System.Text;

namespace BlackJack.Shared;

public class Player
{
    public string Name { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public List<Card> Cards { get; set; } = new();
    public int Score => Cards.Sum(x => x.Value); //кол-во очков сейчас
    public PlayerStatus Status { get; set; } = PlayerStatus.Waiting;
}

public enum PlayerStatus //пока не понимаю, какие нужны
{
    Waiting,
    Playing,
    BlackJack
}