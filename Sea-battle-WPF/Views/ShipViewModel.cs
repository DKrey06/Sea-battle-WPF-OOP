using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Sea_battle_WPF.Core.Enums;

namespace Sea_battle_WPF.ViewModels
{
    public class ShipViewModel
    {
        public int Size { get; set; }
        public ShipDirection Direction { get; set; } = ShipDirection.Horizontal;
        public Point Position { get; set; } = new Point(-1, -1); 
        public bool IsPlaced { get; set; }
        public List<Point> Cells { get; private set; } = new List<Point>();
        public bool IsSelected { get; set; }

        public ShipViewModel(int size)
        {
            Size = size;
            UpdateCells();
        }

        public void Rotate()
        {
            Direction = Direction == ShipDirection.Horizontal
                ? ShipDirection.Vertical
                : ShipDirection.Horizontal;
            UpdateCells();
        }

        public void UpdateCells()
        {
            Cells.Clear();
            for (int i = 0; i < Size; i++)
            {
                if (Direction == ShipDirection.Horizontal)
                {
                    Cells.Add(new Point(Position.X + i, Position.Y));
                }
                else
                {
                    Cells.Add(new Point(Position.X, Position.Y + i));
                }
            }
        }

        public bool ContainsCell(int x, int y)
        {
            return Cells.Any(cell => cell.X == x && cell.Y == y);
        }

        public void MoveTo(Point newPosition)
        {
            Position = newPosition;
            UpdateCells();
        }

        public ShipViewModel Clone()
        {
            return new ShipViewModel(Size)
            {
                Direction = Direction,
                Position = Position,
                IsPlaced = IsPlaced
            };
        }
    }
}