using Sea_battle_WPF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sea_battle_WPF.Core.Models
{
    public class Cell
    {
        public int X {  get; set; }
        public int Y { get; set; }
        public CellState State {  get; set; }
        public Ship Ship { get; set; }

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
            State = CellState.Empty;
        }
    }
}
