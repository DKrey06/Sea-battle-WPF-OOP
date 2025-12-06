using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sea_battle_WPF.Core.Enums
{
    public enum CellState
    {
        Empty,
        Ship,
        Hit,
        Miss,
        Sunk
    }
    public enum ShipDirection
    {
        Vertical,
        Horizontal
    }
    public enum GamePhase
    {
        PlayerTurn,
        ComputerTurn,
        GameOver,
        Setup,
        EnemyTurn
    }
    public enum AILevel
    {
        Easy,
        Hard
    }
    public enum GameMode
    {
        SinglePlayer,     
        HotSeat          
    }
    public enum HotSeatPlayer
    {
        Player1,
        Player2
    }
    public enum AIMode
    {
        RandomSearch,
        TargetTracking,
        FinishShip
    }
}
