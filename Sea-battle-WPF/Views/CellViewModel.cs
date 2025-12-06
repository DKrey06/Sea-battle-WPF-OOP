using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Enums;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;

namespace Sea_battle_WPF.ViewModels
{
    public class CellViewModel : INotifyPropertyChanged
    {
        private readonly Cell _cell;
        private readonly bool _isEnemyField;
        private Brush _cellColor;
        private bool _isClickable;
        private bool _isHighlighted;
        private bool _isShipPlacementValid = true;
        private bool _isDragOver;

        public event PropertyChangedEventHandler PropertyChanged;
        public CellState CellState => _cell.State;
        public int X { get; }
        public int Y { get; }

        public Brush CellColor
        {
            get => _cellColor;
            set
            {
                _cellColor = value;
                OnPropertyChanged();
            }
        }

        public bool IsClickable
        {
            get => _isClickable;
            set
            {
                _isClickable = value;
                OnPropertyChanged();
            }
        }

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                UpdateVisuals();
                OnPropertyChanged();
            }
        }

        public bool IsShipPlacementValid
        {
            get => _isShipPlacementValid;
            set
            {
                _isShipPlacementValid = value;
                UpdateVisuals();
                OnPropertyChanged();
            }
        }

        public bool IsDragOver
        {
            get => _isDragOver;
            set
            {
                _isDragOver = value;
                UpdateVisuals();
                OnPropertyChanged();
            }
        }

        public CellViewModel(int x, int y, Cell cell, bool isEnemyField)
        {
            X = x;
            Y = y;
            _cell = cell;
            _isEnemyField = isEnemyField;

            UpdateFromModel();
            UpdateClickability(false);
        }

        public void UpdateFromModel()
        {
            CellColor = GetCellColor(_cell.State);
            OnPropertyChanged(nameof(CellColor));
        }

        public void UpdateClickability(bool isGameStarted)
        {
            if (_isEnemyField)
            {
                IsClickable = isGameStarted && (_cell.State == CellState.Empty || _cell.State == CellState.Ship);
            }
            else
            {
                IsClickable = false;
            }
        }

        private void UpdateVisuals()
        {
            if (!_isEnemyField && IsHighlighted)
            {
                if (!IsShipPlacementValid)
                {
                    CellColor = Brushes.LightPink;
                }
                else if (IsDragOver)
                {
                    CellColor = Brushes.LightGreen; 
                }
                else
                {
                    CellColor = Brushes.LightYellow; 
                }
            }
            else
            {
                UpdateFromModel();
            }
        }

        private Brush GetCellColor(CellState state)
        {
            if (_isEnemyField)
            {
                return state switch
                {
                    CellState.Hit => (Brush)Application.Current.FindResource("HitBrush"),
                    CellState.Sunk => (Brush)Application.Current.FindResource("HitBrush"),
                    CellState.Miss => Brushes.LightBlue,
                    _ => Brushes.Transparent
                };
            }
            else
            {
                return state switch
                {
                    CellState.Ship => (Brush)Application.Current.FindResource("ShipBrush"),
                    CellState.Hit => (Brush)Application.Current.FindResource("HitBrush"),
                    CellState.Sunk => (Brush)Application.Current.FindResource("HitBrush"),
                    CellState.Miss => Brushes.LightBlue,
                    _ => Brushes.Transparent
                };
            }
        }

        public void ClearHighlight()
        {
            IsHighlighted = false;
            IsDragOver = false;
            UpdateFromModel();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
