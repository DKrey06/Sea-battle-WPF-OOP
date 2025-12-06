using Sea_battle_WPF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sea_battle_WPF.Core.Models
{
    public class Ship
    {
        public int Size {  get; set; }
        public List<Cell> Cells { get; set; } = new List<Cell>();
        public ShipDirection Direction { get; set; }
        public bool IsSunk => Cells.All(c => c.State == CellState.Hit || c.State == CellState.Sunk);

        public Ship(int size)
        {
            Size = size;
        }
    }
}
