using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Enums;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows;

namespace Sea_battle_WPF.ViewModels
{
    public class CellViewModel : INotifyPropertyChanged
    {
        private readonly Cell _cell;
        private readonly bool _isEnemyField;
        private Brush _cellColor;
        private bool _isClickable;

        public event PropertyChangedEventHandler PropertyChanged;

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
        }

        public void UpdateClickability(bool isGameStarted)
        {
            if (_isEnemyField)
            {
                IsClickable = isGameStarted && (_cell.State == CellState.Empty || _cell.State == CellState.Ship);
            }
            else
            {
                IsClickable = false; // Свое поле не кликабельно
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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}