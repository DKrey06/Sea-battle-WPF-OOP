using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sea_battle_WPF.Core.GameAI
{
    public class BakaAI : AIBase
    {
        private List<(int x, int y)> _availableMoves;
        private List<(int x, int y)> _hitCells;

        public BakaAI(GameField playerField) : base(playerField)
        {
            InitializeAvailableMoves();
            _hitCells = new List<(int x, int y)>();
        }

        private void InitializeAvailableMoves()
        {
            _availableMoves = new List<(int x, int y)>();
            for (int x = 0; x < _playerField.Size; x++)
            {
                for (int y = 0; y < _playerField.Size; y++)
                {
                    _availableMoves.Add((x, y));
                }
            }
        }

        public override (int x, int y) MakeMove()
        {
            if (_availableMoves.Count == 0)
            {
                return (-1, -1);
            }

            var randomIndex = _random.Next(0, _availableMoves.Count);
            var move = _availableMoves[randomIndex];
            _availableMoves.RemoveAt(randomIndex);

            var cell = _playerField.Cells[move.x, move.y];
            if (cell.State == CellState.Hit || cell.State == CellState.Sunk)
            {
                _hitCells.Add(move);
            }

            return move;
        }

        public override void Reset()
        {
            InitializeAvailableMoves();
            _hitCells.Clear();
        }
    }
}
