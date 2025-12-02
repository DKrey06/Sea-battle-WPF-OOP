using System;
using System.Collections.Generic;
using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
namespace Sea_battle_WPF.Core.GameAI
{
    public class SmartAI : AIBase
    {
        private AIMode _currentMode = AIMode.RandomSearch;
        public List<Cell> _availableCells;
        private Queue<Cell> _targetQueue = new Queue<Cell>();
        private List<Cell> _hitCells = new List<Cell>();
        private List<Cell> _lastShipCells = new List<Cell>();
        private Cell _lastHit;
        public SmartAI(GameField playerField) : base(playerField)
        {
            InitializeAvailableCells();
        }

        public override (int x, int y) MakeMove()
        {
            Cell targetCell = GetTargetCell();
            if (targetCell != null)
            {
                _availableCells.Remove(targetCell);
                return (targetCell.X, targetCell.Y);
            }

            return (-1, -1);
        }

        public void InitializeAvailableCells()
        {
            _availableCells = new List<Cell>();
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    _availableCells.Add(new Cell(x, y));
                }
            }
        }
        public Cell GetTargetCell()
        {
            switch (_currentMode)
            {
                case AIMode.RandomSearch:
                    return GetRandomTarget();

                case AIMode.TargetTracking:
                    return GetTrackingTarget();

                case AIMode.FinishShip:
                    return GetFinishTarget();

                default:
                    return GetRandomTarget();

            }
        }

        private Cell GetFinishTarget()
        {
            var possibleTargets = GetFinishTargets();
            if (possibleTargets.Count > 0)
            {
                int index = _random.Next(0, possibleTargets.Count);
                var target = possibleTargets[index];
                _targetQueue.Enqueue(target);
                return _targetQueue.Dequeue();
            }
            _currentMode = AIMode.RandomSearch;
            ClearTargetData();
            return GetRandomTarget();
        }

        private List<Cell> GetFinishTargets()
        {
            var targets = new List<Cell>();

            if (_lastHit != null)
            {
                int x = _lastHit.X;
                int y = _lastHit.Y;

                var neighbourCoordinates = new (int dx, int dy)[]
                {
                    (-1, 0), // влево
                    (1, 0), // впарво
                    (0, -1), // вверх
                    (0, 1) // вниз
                };

                foreach (var (dx, dy) in neighbourCoordinates)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    var cell = GetAvailableCell(nx, ny);
                    if (cell != null)
                    {
                        targets.Add(cell);
                    }
                }

                if (_hitCells.Count >= 2)
                {
                    targets = GetDirectionalTargets();
                }

            }
            return targets;
        }

        private List<Cell> GetDirectionalTargets()
        {
            var targets = new List<Cell>();

            _hitCells.Sort((a, b) => a.X.CompareTo(b.X) != 0 ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

            bool isHorizontal = _hitCells[0].Y == _hitCells[1].Y;
            var firstHit = _hitCells[0];
            var lastHit = _hitCells[_hitCells.Count - 1];

            if (isHorizontal)
            {
                var leftCell = GetAvailableCell(firstHit.X - 1, firstHit.Y);
                var rightCell = GetAvailableCell(lastHit.X + 1, lastHit.Y);

                if (leftCell != null)
                {
                    targets.Add(leftCell);
                }
                if (rightCell != null)
                {
                    targets.Add(rightCell);
                }
            }
            else
            {
                var topCell = GetAvailableCell(firstHit.X, firstHit.Y - 1);
                var bottomCell = GetAvailableCell(lastHit.X, lastHit.Y + 1);

                if (topCell != null)
                {
                    targets.Add(topCell);
                }
                if (bottomCell != null)
                {
                    targets.Add(bottomCell);
                }
            }

            return targets;
        }

        private Cell GetAvailableCell(int x, int y)
        {
            if (x < 0 || x >= 10 || y < 0 || y >= 10)
                return null;

            var cell = _availableCells.FirstOrDefault(c => c.X == x && c.Y == y);

            if (cell != null)
            {
                var fieldCell = _playerField.Cells[x, y];
                if (fieldCell.State == CellState.Empty || fieldCell.State == CellState.Ship)
                {
                    return cell;
                }
            }

            return null;
        }

        private Cell GetTrackingTarget()
        {
            if (_targetQueue.Count > 0)
            {
                return _targetQueue.Dequeue();
            }
            _currentMode = AIMode.FinishShip;
            return GetFinishTarget();
        }

        private Cell GetRandomTarget()
        {
            if (_availableCells.Count == 0)
            {
                return null;
            }
            int index = _random.Next(0, _availableCells.Count);
            return _availableCells[index];
        }

        public void ProcessShotResult(int x, int y, CellState result)
        {
            var cell = _playerField.Cells[x, y];
            if (result == CellState.Hit || result == CellState.Sunk)
            {
                _hitCells.Add(cell);
                _lastHit = cell;
                if (_currentMode == AIMode.RandomSearch)
                {
                    _currentMode = AIMode.TargetTracking;
                }

                if (result == CellState.Sunk)
                {
                    MarkShipAsDestroyed();
                    ClearTargetData();

                    if (_hitCells.Count == 0) 
                    { 
                        _currentMode = AIMode.RandomSearch;
                    }
                    else
                    {
                        _currentMode = AIMode.TargetTracking;
                    }
                }               
            }


            else if(result == CellState.Miss && _currentMode == AIMode.TargetTracking)
            {
                _currentMode = AIMode.FinishShip;
            }
        }

        private void MarkShipAsDestroyed()
        {
            foreach (var hit in _hitCells)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = hit.X + dx;
                        int ny = hit.Y + dy;

                        if (nx >= 0 && nx < 10 && ny >= 0 && ny < 10)
                        {
                            var cellToRemove = _availableCells.FirstOrDefault(c => c.X == nx && c.Y == ny);
                            if (cellToRemove != null)
                            {
                                _availableCells.Remove(cellToRemove);
                            }
                        }
                    }
                }
            }

            _lastShipCells.AddRange(_hitCells);
            _hitCells.Clear();
            _lastHit = null;
            _targetQueue.Clear();
        }
        private void ClearTargetData()
        {
            _hitCells.Clear();
            _lastHit = null;
            _targetQueue.Clear();
        }
        public override void Reset()
        {
            _currentMode = AIMode.RandomSearch;
            InitializeAvailableCells();
            ClearTargetData();
            _lastShipCells.Clear();
        }
    }
}