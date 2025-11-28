using Sea_battle_WPF.Core;
using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Services;
using SeaBattle.Presentation.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace Sea_battle_WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Game _game;
        private string _gameStatus;

        public event PropertyChangedEventHandler PropertyChanged;

        public Game Game => _game;

        public string GameStatus
        {
            get => _gameStatus;
            set
            {
                _gameStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsGameStarted { get; private set; }
        public bool CanStartGame => !IsGameStarted && _game.PlayerField.Ships.Count == 10;
        public bool CanArrangeShips => !IsGameStarted;

        public ObservableCollection<CellViewModel> PlayerCells { get; } = new ObservableCollection<CellViewModel>();
        public ObservableCollection<CellViewModel> EnemyCells { get; } = new ObservableCollection<CellViewModel>();

        public ICommand AutoArrangeCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand CellClickCommand { get; }

        public MainViewModel()
        {
            var shipService = new ShipPlacmentService();
            _game = new Game(shipService);

            GameStatus = "Расставьте корабли";

            InitializeCells();
            AutoArrangeEnemyShips();

            AutoArrangeCommand = new RelayCommand(AutoArrangeExecute, () => CanArrangeShips);
            StartGameCommand = new RelayCommand(StartGameExecute, () => CanStartGame);
            CellClickCommand = new RelayCommand<CellViewModel>(CellClickExecute);
        }

        private void InitializeCells()
        {
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    PlayerCells.Add(new CellViewModel(x, y, _game.PlayerField.Cells[x, y], false));
                    EnemyCells.Add(new CellViewModel(x, y, _game.EnemyField.Cells[x, y], true));
                }
            }
        }

        private void AutoArrangeEnemyShips()
        {
            _game.AutoArrangeEnemyShips();
            UpdateEnemyCells();
        }
        private void AutoArrangeExecute()
        {
            _game.AutoArrangePlayerShips();
            UpdatePlayerCells();
            GameStatus = "Корабли расставлены";

            OnPropertyChanged(nameof(CanStartGame));
        }

        private void StartGameExecute()
        {
            _game.StartGame();
            IsGameStarted = true;
            GameStatus = "Ваш ход";

            OnPropertyChanged(nameof(IsGameStarted));
            OnPropertyChanged(nameof(CanStartGame));
            OnPropertyChanged(nameof(CanArrangeShips));
            UpdateEnemyCellsClickability();
        }

        private void CellClickExecute(CellViewModel cellVM)
        {
            if (!IsGameStarted || !cellVM.IsClickable) return;

            var result = _game.EnemyField.Shoot(cellVM.X, cellVM.Y);
            cellVM.UpdateFromModel();

            if (_game.EnemyField.AllShipsSunk)
            {
                GameStatus = "Вы победили!";
                DisableAllEnemyCells();
            }
            else if (result == CellState.Miss)
            {
                GameStatus = "Ход противника";
            }
        }

        private void UpdatePlayerCells()
        {
            foreach (var cellVM in PlayerCells)
            {
                cellVM.UpdateFromModel();
            }
        }

        private void UpdateEnemyCells()
        {
            foreach (var cellVM in EnemyCells)
            {
                cellVM.UpdateFromModel();
            }
        }

        private void UpdateEnemyCellsClickability()
        {
            foreach (var cellVM in EnemyCells)
            {
                cellVM.UpdateClickability(IsGameStarted);
            }
        }

        private void DisableAllEnemyCells()
        {
            foreach (var cellVM in EnemyCells)
            {
                cellVM.IsClickable = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}