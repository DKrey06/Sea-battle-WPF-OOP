using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Sea_battle_WPF.Core.Enums;

namespace Sea_battle_WPF.Core.Models
{
    public class GameField
    {
        public Cell[,] Cells {get; private set;}
        public List<Ship> Ships {get; private set;} = new List<Ship>();
        public int Size { get; private set; }
        public bool AllShipsSunk => Ships.All(s => s.IsSunk);

        public GameField(int size = 10)
        {
            Size = size;
            Cells = new Cell[Size, Size];
            InitializeCells();
        }
        private void InitializeCells()
        {
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    Cells[x, y] = new Cell(x, y);
                }
            }
        }
        public bool CanPlaceShip(int startX, int startY, int size, ShipDirection direction)
        {
            for(int i = 0; i < size; i++)
            {
                int x = direction == ShipDirection.Horizontal ? startX + i : startX;
                int y = direction == ShipDirection.Vertical ? startY + i : startY;

                if (x >= Size || y >= Size || Cells[x, y].State != CellState.Empty)
                {
                    return false;
                }
                
                for(int dx = -1; dx<=1; dx++)
                {
                    for (int dy = -1; dy<=1; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;


                        if (nx >= 0 && nx < Size && ny >= 0 && ny < Size)
                        {
                            if (Cells[nx, ny].State == CellState.Ship)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            
            return true;
        }
        
        public void PlaceShip(int startX, int startY, int size, ShipDirection direction)
        {
            var ship = new Ship(size) { Direction = direction };
            
            for (int i = 0; i < size; i++)
            {
                int x = direction == ShipDirection.Horizontal ? startX + i : startX;
                int y = direction == ShipDirection.Vertical ? startY + i : startY;

                var cell = Cells[x, y];
                cell.State = CellState.Ship;
                cell.Ship = ship;
                ship.Cells.Add(cell);

            }
            Ships.Add(ship);
        }
        public CellState Shoot(int x, int y)
        {
            var cell = Cells[x, y];
            if (cell.State == CellState.Ship)
            {
                cell.State = CellState.Hit;

                if (cell.Ship.IsSunk)
                {
                    foreach (var shipCell in cell.Ship.Cells)
                    {
                        shipCell.State = CellState.Sunk;
                    }
                    return CellState.Sunk;
                }
                return CellState.Hit;
            }
            else if (cell.State == CellState.Empty)
            {
                cell.State = CellState.Miss;
                return CellState.Miss;
            }
            return cell.State;
        }
    }
    
}
