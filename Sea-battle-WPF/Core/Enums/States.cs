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
}
