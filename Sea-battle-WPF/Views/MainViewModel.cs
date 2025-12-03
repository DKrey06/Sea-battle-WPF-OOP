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
using System.Linq;
using System.Collections.Generic;
using System;

namespace Sea_battle_WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Game _game;
        private readonly ManualShipPlacementService _manualPlacementService;
        private string _gameStatus;
        private AILevel _selectedAILevel = AILevel.Hard;
        private ShipViewModel _selectedShip;
        private bool _isManualPlacementMode = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public Game Game => _game;
        public ManualShipPlacementService ManualPlacementService => _manualPlacementService;

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

        public bool IsManualPlacementMode
        {
            get => _isManualPlacementMode;
            set
            {
                _isManualPlacementMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartGame));
            }
        }

        public bool IsManualPlacementValid { get; private set; }

        public ShipViewModel SelectedShip
        {
            get => _selectedShip;
            set
            {
                _selectedShip = value;
                OnPropertyChanged();
                ClearHighlights();
                if (value != null)
                {
                    foreach (var ship in AvailableShips)
                    {
                        ship.IsSelected = ship == value;
                    }
                }
            }
        }

        public ObservableCollection<ShipViewModel> AvailableShips { get; } = new ObservableCollection<ShipViewModel>();
        public ObservableCollection<CellViewModel> PlayerCells { get; } = new ObservableCollection<CellViewModel>();
        public ObservableCollection<CellViewModel> EnemyCells { get; } = new ObservableCollection<CellViewModel>();

        public bool IsEasyAI => SelectedAILevel == AILevel.Easy;
        public bool IsSmartAI => SelectedAILevel == AILevel.Hard;
        public bool IsGameStarted { get; private set; }
        public bool CanStartGame => !IsGameStarted && (_game.PlayerField.Ships.Count == 10 || IsManualPlacementValid);
        public bool CanArrangeShips => !IsGameStarted;
        public bool CanChangeAILevel => !IsGameStarted;
        private RelayCommand resetManualPlacementCommand;
        public ICommand ResetManualPlacementCommand => resetManualPlacementCommand ??= new RelayCommand(ResetManualPlacementExecute);
        public ICommand AutoArrangeCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand CellClickCommand { get; }
        public ICommand CellMouseEnterCommand { get; }
        public ICommand CellMouseLeaveCommand { get; }
        public ICommand SurrenderCommand { get; }
        public ICommand SetEasyAICommand { get; }
        public ICommand SetSmartAICommand { get; }
        public ICommand RotateShipCommand { get; }
        public ICommand RemoveShipCommand { get; }
        public ICommand SelectShipCommand { get; }
        public ICommand SwitchToManualModeCommand { get; }
        public ICommand SwitchToAutoModeCommand { get; }
        public ICommand PlayerCellClickCommand { get; }
        public ICommand PlayerCellMouseEnterCommand { get; }
        public ICommand PlayerCellMouseLeaveCommand { get; }

        public MainViewModel()
        {
            var shipService = new ShipPlacmentService();
            _game = new Game(shipService);

            var availableShipsList = new List<ShipViewModel>();
            _manualPlacementService = new ManualShipPlacementService(_game.PlayerField, availableShipsList);
            SubscribeToManualPlacementEvents();

            GameStatus = "Расставьте корабли вручную или автоматически";

            InitializeCells();
            InitializeAvailableShips(availableShipsList);
            AutoArrangeEnemyShips();

            AutoArrangeCommand = new RelayCommand(AutoArrangeExecute, () => CanArrangeShips);
            StartGameCommand = new RelayCommand(StartGameExecute, () => CanStartGame);
            CellClickCommand = new RelayCommand<CellViewModel>(CellClickExecute);
            CellMouseEnterCommand = new RelayCommand<CellViewModel>(CellMouseEnterExecute);
            CellMouseLeaveCommand = new RelayCommand<CellViewModel>(CellMouseLeaveExecute);
            SurrenderCommand = new RelayCommand(SurrenderExecute);
            SetEasyAICommand = new RelayCommand(() => SelectedAILevel = AILevel.Easy, () => CanChangeAILevel);
            SetSmartAICommand = new RelayCommand(() => SelectedAILevel = AILevel.Hard, () => CanChangeAILevel);
            RotateShipCommand = new RelayCommand(RotateShipExecute);
            RemoveShipCommand = new RelayCommand<ShipViewModel>(RemoveShipExecute);
            SelectShipCommand = new RelayCommand<ShipViewModel>(SelectShipExecute);
            SwitchToManualModeCommand = new RelayCommand(SwitchToManualModeExecute);
            SwitchToAutoModeCommand = new RelayCommand(SwitchToAutoModeExecute);
            PlayerCellClickCommand = new RelayCommand<CellViewModel>(PlayerCellClickExecute);
            PlayerCellMouseEnterCommand = new RelayCommand<CellViewModel>(PlayerCellMouseEnterExecute);
            PlayerCellMouseLeaveCommand = new RelayCommand<CellViewModel>(PlayerCellMouseLeaveExecute);
        }

        private void SubscribeToManualPlacementEvents()
        {
            _manualPlacementService.ShipPlaced += OnShipPlaced;
            _manualPlacementService.ShipRemoved += OnShipRemoved;
            _manualPlacementService.PlacementValidated += OnPlacementValidated;
            _manualPlacementService.HighlightCellsRequested += OnHighlightCellsRequested;
            _manualPlacementService.ClearHighlightRequested += OnClearHighlightRequested;
        }

        private void InitializeAvailableShips(List<ShipViewModel> availableShipsList)
        {
            AvailableShips.Clear();

            _manualPlacementService.InitializeAvailableShips();

            foreach (var ship in availableShipsList)
            {
                AvailableShips.Add(ship);
            }
        }
        private void PlayerCellClickExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && IsManualPlacementMode && !SelectedShip.IsPlaced)
            {
                if (_manualPlacementService.TryPlaceShip(SelectedShip, cellVM.X, cellVM.Y))
                {
                    UpdatePlayerCells();

                    SelectedShip.IsPlaced = true;

                    var nextShip = _manualPlacementService.GetUnplacedShips().FirstOrDefault();
                    SelectedShip = nextShip;

                    var tempList = AvailableShips.ToList();
                    AvailableShips.Clear();
                    foreach (var item in tempList)
                    {
                        AvailableShips.Add(item);
                    }
                }
            }
        }
        private void InitializeCells()
        {
            PlayerCells.Clear();
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    PlayerCells.Add(new CellViewModel(x, y, _game.PlayerField.Cells[x, y], false));
                    EnemyCells.Add(new CellViewModel(x, y, _game.EnemyField.Cells[x, y], true));
                }
            }
        }

        private void SwitchToManualModeExecute()
        {
            IsManualPlacementMode = true;
            GameStatus = "Режим ручной расстановки. Выберите корабль и разместите его на поле.";
        }

        private void SwitchToAutoModeExecute()
        {
            IsManualPlacementMode = false;
            GameStatus = "Режим автоматической расстановки.";
        }

        private void SelectShipExecute(ShipViewModel ship)
        {
            if (ship == null) return;

            if (SelectedShip == ship)
            {
                SelectedShip = null;
                ship.IsSelected = false;

                var tempList = AvailableShips.ToList();
                AvailableShips.Clear();
                foreach (var item in tempList)
                {
                    AvailableShips.Add(item);
                }
                return;
            }

            if (ship.IsPlaced)
            {
                foreach (var availableShip in AvailableShips)
                {
                    availableShip.IsSelected = false;
                }

                SelectedShip = ship;
                ship.IsSelected = true;

                var tempList = AvailableShips.ToList();
                AvailableShips.Clear();
                foreach (var item in tempList)
                {
                    AvailableShips.Add(item);
                }
                return;
            }

            foreach (var availableShip in AvailableShips)
            {
                availableShip.IsSelected = false;
            }

            SelectedShip = ship;
            ship.IsSelected = true;

            var tempList2 = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList2)
            {
                AvailableShips.Add(item);
            }
        }

        private void RotateShipExecute()
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced)
            {
                SelectedShip.Rotate();
                OnPropertyChanged(nameof(SelectedShip));
            }
        }

        private void RemoveShipExecute(ShipViewModel ship)
        {
            if (ship == null || !ship.IsPlaced) return;

            _manualPlacementService.RemoveShip(ship);

            SelectedShip = null;
            ship.IsSelected = false;

            var tempList = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList)
            {
                AvailableShips.Add(item);
            }
        }

        private void CellMouseEnterExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && IsManualPlacementMode)
            {
                _manualPlacementService.PreviewShipPlacement(SelectedShip, cellVM.X, cellVM.Y, PlayerCells.ToList());
            }
        }

        private void CellMouseLeaveExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced)
            {
                _manualPlacementService.ClearHighlight();
            }
        }

        private void CellClickExecute(CellViewModel cellVM)
        {
            if (IsGameStarted)
            {
                HandleGameTurn(cellVM);
            }
            else if (SelectedShip != null && IsManualPlacementMode && !SelectedShip.IsPlaced)
            {
                if (_manualPlacementService.TryPlaceShip(SelectedShip, cellVM.X, cellVM.Y))
                {
                    UpdatePlayerCells();

                    var nextShip = _manualPlacementService.GetUnplacedShips().FirstOrDefault();
                    SelectedShip = nextShip;
                }
            }
        }
        private void AutoArrangeEnemyShips()
        {
            _game.AutoArrangeEnemyShips();
            UpdateEnemyCells();
        }
        private void ResetManualPlacementExecute()
        {
            _manualPlacementService.ResetPlacement();

            _game.PlayerField.ClearField();

            UpdatePlayerCells();

            SelectedShip = null;

            foreach (var ship in AvailableShips)
            {
                ship.IsSelected = false;
                ship.IsPlaced = false;
            }
            var tempList = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList)
            {
                AvailableShips.Add(item);
            }

            IsManualPlacementValid = false;

            GameStatus = "Все корабли сброшены. Выберите корабль для размещения.";

            OnPropertyChanged(nameof(IsManualPlacementValid));
            OnPropertyChanged(nameof(CanStartGame));
        }
        private void AutoArrangeExecute()
        {
            _manualPlacementService.ResetPlacement();

            _game.PlayerField.ClearField();

            _game.AutoArrangePlayerShips();
            UpdatePlayerCells();

            IsManualPlacementMode = false;
            GameStatus = "Корабли расставлены автоматически";

            OnPropertyChanged(nameof(CanStartGame));
            OnPropertyChanged(nameof(IsManualPlacementMode));
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
        private void PlayerCellMouseEnterExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && IsManualPlacementMode)
            {
                _manualPlacementService.PreviewShipPlacement(SelectedShip, cellVM.X, cellVM.Y, PlayerCells.ToList());
            }
        }

        private void PlayerCellMouseLeaveExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced)
            {
                _manualPlacementService.ClearHighlight();
            }
        }
        private void HandleGameTurn(CellViewModel cellVM)
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

        private void OnShipPlaced(ShipViewModel ship)
        {
            UpdatePlayerCells();

            var shipInList = AvailableShips.FirstOrDefault(s => s == ship);
            if (shipInList != null)
            {
                shipInList.IsPlaced = true;
            }

            var tempList = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList)
            {
                AvailableShips.Add(item);
            }
        }

        private void OnShipRemoved(ShipViewModel ship)
        {

            UpdatePlayerCells();

            ship.IsSelected = false;
            if (SelectedShip == ship)
            {
                SelectedShip = null;
            }

            var tempList = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList)
            {
                AvailableShips.Add(item);
            }
        }

        private void OnPlacementValidated(bool isValid)
        {
            IsManualPlacementValid = isValid;
            OnPropertyChanged(nameof(IsManualPlacementValid));
            OnPropertyChanged(nameof(CanStartGame));
        }

        private void OnHighlightCellsRequested(List<CellViewModel> cells)
        {
            // Уже обрабатывается в CellViewModel
        }

        private void OnClearHighlightRequested()
        {
            ClearHighlights();
        }

        private void ClearHighlights()
        {
            foreach (var cell in PlayerCells)
            {
                cell.ClearHighlight();
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
            _manualPlacementService.ResetPlacement();
            UpdatePlayerCells();
            UpdateEnemyCells();

            SelectedShip = null;

            foreach (var ship in AvailableShips)
            {
                ship.IsSelected = false;
                ship.IsPlaced = false;
            }

            _game.AutoArrangeEnemyShips();
            UpdateEnemyCells();
            IsGameStarted = false;
            IsManualPlacementMode = true;
            GameStatus = "Расставьте корабли";

            OnPropertyChanged(nameof(IsGameStarted));
            OnPropertyChanged(nameof(IsManualPlacementMode));
            OnPropertyChanged(nameof(CanArrangeShips));
            OnPropertyChanged(nameof(CanStartGame));
            OnPropertyChanged(nameof(CanChangeAILevel));
            OnPropertyChanged(nameof(IsEasyAI));
            OnPropertyChanged(nameof(IsSmartAI));
            OnPropertyChanged(nameof(IsManualPlacementValid));
        }
        private void UpdateAISettenigsText()
        {
            OnPropertyChanged(nameof(IsEasyAI));
            OnPropertyChanged(nameof(IsSmartAI));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}