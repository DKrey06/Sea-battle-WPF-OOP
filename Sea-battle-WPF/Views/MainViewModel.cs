using Sea_battle_WPF.Core;
using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Services;
using SeaBattle.Presentation.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Sea_battle_WPF.Core.GameAI;

namespace Sea_battle_WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Game _game;
        private string _gameStatus;
        private AILevel _selectedAILevel = AILevel.Hard;

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

        public string SelectedAILevelDisplay
        {
            get
            {
                return SelectedAILevel switch
                {
                    AILevel.Easy => "Простой",
                    AILevel.Hard => "Умный",
                    _ => SelectedAILevel.ToString()
                };
            }
        }

        public AILevel SelectedAILevel
        {
            get => _selectedAILevel;

            set
            {
                if (_selectedAILevel != value)
                {
                    _selectedAILevel = value;
                    _game.SetAILevel(value);
                    OnPropertyChanged(nameof(IsEasyAI));
                    OnPropertyChanged(nameof(IsSmartAI));
                    OnPropertyChanged(nameof(SelectedAILevelDisplay));
                    UpdateAISettenigsText();
                }
            }
        }
        private void UpdateAISettenigsText()
        {
            OnPropertyChanged(nameof(IsEasyAI));
            OnPropertyChanged(nameof(IsSmartAI));
        }

        public bool IsEasyAI => SelectedAILevel == AILevel.Easy;
        public bool IsSmartAI => SelectedAILevel == AILevel.Hard;
        public bool IsGameStarted { get; private set; }
        public bool CanStartGame => !IsGameStarted && _game.PlayerField.Ships.Count == 10;
        public bool CanArrangeShips => !IsGameStarted;
        public bool CanChangeAILevel => !IsGameStarted;

        public ObservableCollection<CellViewModel> PlayerCells { get; } = new ObservableCollection<CellViewModel>();
        public ObservableCollection<CellViewModel> EnemyCells { get; } = new ObservableCollection<CellViewModel>();

        public ICommand AutoArrangeCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand CellClickCommand { get; }
        public ICommand SurrenderCommand { get; }
        public ICommand SetEasyAICommand { get; }
        public ICommand SetSmartAICommand { get; }

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
            SurrenderCommand = new RelayCommand(SurrenderExecute);
            SetEasyAICommand = new RelayCommand(() => SelectedAILevel = AILevel.Easy, () => CanChangeAILevel);
            SetSmartAICommand = new RelayCommand(() => SelectedAILevel = AILevel.Hard, () => CanChangeAILevel);
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
            if (!IsGameStarted || !cellVM.IsClickable || _game.EnemyField.AllShipsSunk) return;

            var result = _game.EnemyField.Shoot(cellVM.X, cellVM.Y);
            cellVM.UpdateFromModel();

            if (_game.EnemyField.AllShipsSunk)
            {
                GameStatus = "Вы победили!";
                DisableAllEnemyCells();
                IsGameStarted = false;
                MessageBoxResult _result = MessageBox.Show("Хотите сыграть снова?", "Победа", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (_result == MessageBoxResult.Yes)
                {
                    GameStatus = "Расставьте корабли";
                    ResetGame();
                }
                OnPropertyChanged(nameof(IsGameStarted));
                OnPropertyChanged(nameof(CanArrangeShips));
                return; 
            }

            else if (result == CellState.Miss)
            {
                GameStatus = "Ход противника";
                _game.CurrentPhase = GamePhase.EnemyTurn;
                DisableAllEnemyCells();

                Task.Delay(500).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MakeAIMove();
                    });
                });
            }

            else if (result == CellState.Hit || result == CellState.Sunk) 
            {
                GameStatus = "Попадание! Продолжайте";

                if (result == CellState.Sunk)
                {
                    UpdateEnemyCells();
                }
            }
        }
        private void MakeAIMove()
        {
            _game.MakeAIMove();
            UpdatePlayerCells();

            if (_game.PlayerField.AllShipsSunk)
            {
                GameStatus = "Вы проиграли!";
                IsGameStarted = false;
                MessageBoxResult _result = MessageBox.Show("Хотите сыграть снова?", "Поражение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (_result == MessageBoxResult.Yes)
                {
                    GameStatus = "Расставьте корабли";
                    ResetGame();
                }
                OnPropertyChanged(nameof(IsGameStarted));
                OnPropertyChanged(nameof(CanArrangeShips));
            }
            else
            {
                if (_game.LastAIMoveWasHit)
                {
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MakeAIMove(); 
                        });
                    });
                }
                else
                {
                    GameStatus = "Ваш ход";
                    UpdateEnemyCellsClickability();
                }
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
            if (_game.EnemyField.AllShipsSunk) return;

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

        private void SurrenderExecute()
        {
            if (!IsGameStarted) return;

            var result = MessageBox.Show("Вы уверены, что хотите сдаться?", "Сдаться", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                GameStatus = "Вы сдались";
                ResetGame();

            }
        }
        public void ResetGame()
        {
            _game.ResetGame();
            _game.AutoArrangeEnemyShips();
            PlayerCells.Clear();
            EnemyCells.Clear();

            InitializeCells();
            UpdatePlayerCells();
            UpdateEnemyCells();
            
            IsGameStarted = false;
            GameStatus = "Расставьте корабли";

            OnPropertyChanged(nameof(IsGameStarted));
            OnPropertyChanged(nameof(CanArrangeShips));
            OnPropertyChanged(nameof(CanStartGame));
            OnPropertyChanged(nameof(CanChangeAILevel));
            OnPropertyChanged(nameof(IsEasyAI));
            OnPropertyChanged(nameof(IsSmartAI));

        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}