using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Sea_battle_WPF.Core.Services
{
    public class ManualShipPlacementService
    {
        private readonly GameField _field;
        private readonly List<ShipViewModel> _availableShips;
        private readonly Dictionary<int, int> _shipRequirements = new Dictionary<int, int>
        {
            { 4, 1 },
            { 3, 2 },
            { 2, 3 },
            { 1, 4 }
        };

        public event Action<ShipViewModel> ShipPlaced;
        public event Action<ShipViewModel> ShipRemoved;
        public event Action<bool> PlacementValidated;
        public event Action<List<CellViewModel>> HighlightCellsRequested;
        public event Action ClearHighlightRequested;

        public ManualShipPlacementService(GameField field, List<ShipViewModel> availableShips)
        {
            _field = field;
            _availableShips = availableShips;
        }

        public void InitializeAvailableShips()
        {
            _availableShips.Clear();

            foreach (var shipType in _shipRequirements)
            {
                for (int i = 0; i < shipType.Value; i++)
                {
                    _availableShips.Add(new ShipViewModel(shipType.Key));
                }
            }
        }

        public bool TryPlaceShip(ShipViewModel ship, int x, int y)
        {
            if (ship == null || ship.IsPlaced)
                return false;

            if (!CanPlaceShip(x, y, ship.Size, ship.Direction))
                return false;

            _field.PlaceShip(x, y, ship.Size, ship.Direction);

            ship.MoveTo(new Point(x, y));
            ship.IsPlaced = true;

            ShipPlaced?.Invoke(ship);
            ValidatePlacement();

            return true;
        }

        public void RemoveShip(ShipViewModel ship)
        {
            if (ship == null || !ship.IsPlaced)
                return;

            foreach (var cell in ship.Cells)
            {
                int cellX = (int)cell.X;
                int cellY = (int)cell.Y;

                if (cellX >= 0 && cellX < _field.Size && cellY >= 0 && cellY < _field.Size)
                {
                    var cellModel = _field.Cells[cellX, cellY];
                    cellModel.State = CellState.Empty;
                    cellModel.Ship = null;
                }
            }

            var shipToRemove = _field.Ships.FirstOrDefault(s =>
                s.Size == ship.Size &&
                s.Cells.Any(c => c.X == (int)ship.Position.X && c.Y == (int)ship.Position.Y));

            if (shipToRemove != null)
            {
                _field.Ships.Remove(shipToRemove);
            }

            ship.IsPlaced = false;
            ship.Position = new Point(-1, -1);

            ShipRemoved?.Invoke(ship);
            ValidatePlacement();
        }

        public bool CanPlaceShip(int x, int y, int size, ShipDirection direction)
        {
            return _field.CanPlaceShip(x, y, size, direction);
        }

        public void PreviewShipPlacement(ShipViewModel ship, int x, int y, List<CellViewModel> cellViewModels)
        {
            if (ship == null || ship.IsPlaced || x < 0 || y < 0 || x >= _field.Size || y >= _field.Size)
            {
                ClearHighlightRequested?.Invoke();
                return;
            }

            var cellsToHighlight = new List<CellViewModel>();
            bool isValid = true;

            for (int i = 0; i < ship.Size; i++)
            {
                int checkX = ship.Direction == ShipDirection.Horizontal ? x + i : x;
                int checkY = ship.Direction == ShipDirection.Horizontal ? y : y + i;

                if (checkX >= _field.Size || checkY >= _field.Size)
                {
                    isValid = false;
                    break;
                }

                var cellVM = cellViewModels.FirstOrDefault(c => c.X == checkX && c.Y == checkY);
                if (cellVM != null)
                {
                    cellsToHighlight.Add(cellVM);

                    if (!CanPlaceShip(checkX, checkY, 1, ShipDirection.Horizontal) ||
                        _field.Cells[checkX, checkY].State == CellState.Ship)
                    {
                        isValid = false;
                    }
                }
            }

            foreach (var cellVM in cellsToHighlight)
            {
                cellVM.IsHighlighted = true;
                cellVM.IsShipPlacementValid = isValid;
            }

            HighlightCellsRequested?.Invoke(cellsToHighlight);
        }

        public void ClearHighlight()
        {
            ClearHighlightRequested?.Invoke();
        }

        public void ValidatePlacement()
        {
            bool isValid = true;

            var placedShipsBySize = _availableShips
                .Where(s => s.IsPlaced)
                .GroupBy(s => s.Size)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var requirement in _shipRequirements)
            {
                if (!placedShipsBySize.ContainsKey(requirement.Key) ||
                    placedShipsBySize[requirement.Key] != requirement.Value)
                {
                    isValid = false;
                    break;
                }
            }

            if (_availableShips.Count(s => s.IsPlaced) != 10)
            {
                isValid = false;
            }

            PlacementValidated?.Invoke(isValid);
        }

        public void ResetPlacement()
        {
            foreach (var ship in _availableShips.Where(s => s.IsPlaced).ToList())
            {
                RemoveShip(ship);
            }

            _field.ClearField();

            foreach (var ship in _availableShips)
            {
                ship.IsPlaced = false;
                ship.Position = new Point(-1, -1);
                ship.Direction = ShipDirection.Horizontal;
                ship.IsSelected = false;
                ship.UpdateCells();
            }

            ValidatePlacement();
        }

        public List<ShipViewModel> GetUnplacedShips()
        {
            return _availableShips.Where(s => !s.IsPlaced).ToList();
        }

        public List<ShipViewModel> GetPlacedShips()
        {
            return _availableShips.Where(s => s.IsPlaced).ToList();
        }

        public bool IsAllShipsPlaced()
        {
            return _availableShips.All(s => s.IsPlaced);
        }
    }
}