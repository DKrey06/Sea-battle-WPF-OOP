using Sea_battle_WPF.Core;
using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Services;
using Sea_battle_WPF.ViewModels;
using SeaBattle.Presentation.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Sea_battle_WPF.ViewModels
{
    public class HotSeatViewModel : INotifyPropertyChanged
    {
        private GameField _player1Field;
        private GameField _player2Field;
        private readonly ShipPlacmentService _shipService;
        private ManualShipPlacementService _player1ManualService;
        private ManualShipPlacementService _player2ManualService;
        private List<ShipViewModel> _player1Ships = new List<ShipViewModel>();
        private List<ShipViewModel> _player2Ships = new List<ShipViewModel>();

        private string _gameStatus;
        private bool _isGameStarted;
        private bool _isPlayer1Turn = true;
        private bool _isPlayer1Ready = false;
        private bool _isPlayer2Ready = false;
        private bool _isDeviceLocked = false;
        private ShipViewModel _selectedShip;
        private bool _isCurrentPlacementValid = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public string GameStatus
        {
            get => _gameStatus;
            set
            {
                _gameStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsGameStarted
        {
            get => _isGameStarted;
            set
            {
                _isGameStarted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPlayer1Turn));
                OnPropertyChanged(nameof(IsPlayer2Turn));
                OnPropertyChanged(nameof(CanStartGame));
                OnPropertyChanged(nameof(ShowGameControls));
                OnPropertyChanged(nameof(ShowSetupControls));
                OnPropertyChanged(nameof(CurrentPlayerName));
                OnPropertyChanged(nameof(CurrentEnemyName));
                OnPropertyChanged(nameof(MyFieldLabel));
                OnPropertyChanged(nameof(EnemyFieldLabel));
                OnPropertyChanged(nameof(CurrentPlacementTitle));
                OnPropertyChanged(nameof(ShouldShowShipPanel));
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool IsPlayer1Turn
        {
            get => _isGameStarted && _isPlayer1Turn;
            set
            {
                _isPlayer1Turn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPlayer2Turn));
                OnPropertyChanged(nameof(CurrentPlayerName));
                OnPropertyChanged(nameof(CurrentEnemyName));
                OnPropertyChanged(nameof(MyFieldLabel));
                OnPropertyChanged(nameof(EnemyFieldLabel));
                OnPropertyChanged(nameof(CurrentPlacementTitle));
                OnPropertyChanged(nameof(ShouldShowShipPanel));
                OnPropertyChanged(nameof(CurrentPlayerReady));
                OnPropertyChanged(nameof(IsCurrentPlacementValid));
                OnPropertyChanged(nameof(AvailableShips));
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool IsPlayer2Turn => _isGameStarted && !_isPlayer1Turn;

        public string CurrentPlayerName
        {
            get
            {
                if (IsGameStarted)
                    return IsPlayer1Turn ? "Игрок 1" : "Игрок 2";
                else
                    return !IsPlayer1Ready ? "Игрок 1" : "Игрок 2";
            }
        }

        public string CurrentEnemyName
        {
            get
            {
                if (IsGameStarted)
                    return IsPlayer1Turn ? "Игрок 2" : "Игрок 1";
                else
                    return !IsPlayer1Ready ? "Игрок 2" : "Игрок 1";
            }
        }

        public string MyFieldLabel
        {
            get
            {
                if (IsGameStarted)
                    return IsPlayer1Turn ? "Мое поле (Игрок 1)" : "Мое поле (Игрок 2)";
                else
                    return !IsPlayer1Ready ? "Поле Игрока 1" : "Поле Игрока 2";
            }
        }

        public string EnemyFieldLabel
        {
            get
            {
                if (IsGameStarted)
                    return IsPlayer1Turn ? "Поле Игрока 2" : "Поле Игрока 1";
                else
                    return "Поле противника";
            }
        }

        public string CurrentPlacementTitle
        {
            get
            {
                if (IsGameStarted) return "";
                if (!IsPlayer1Ready) return "Игрок 1: Ручная расстановка";
                if (!IsPlayer2Ready) return "Игрок 2: Ручная расстановка";
                return "Оба игрока готовы";
            }
        }

        public bool IsPlayer1Ready
        {
            get => _isPlayer1Ready;
            set
            {
                _isPlayer1Ready = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartGame));
                OnPropertyChanged(nameof(CurrentPlayerReady));
                OnPropertyChanged(nameof(ShouldShowShipPanel));
                OnPropertyChanged(nameof(CurrentPlacementTitle));
                OnPropertyChanged(nameof(CurrentPlayerName));
                OnPropertyChanged(nameof(CurrentEnemyName));
                OnPropertyChanged(nameof(MyFieldLabel));
                OnPropertyChanged(nameof(EnemyFieldLabel));
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool IsPlayer2Ready
        {
            get => _isPlayer2Ready;
            set
            {
                _isPlayer2Ready = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartGame));
                OnPropertyChanged(nameof(CurrentPlayerReady));
                OnPropertyChanged(nameof(ShouldShowShipPanel));
                OnPropertyChanged(nameof(CurrentPlacementTitle));
                OnPropertyChanged(nameof(CurrentPlayerName));
                OnPropertyChanged(nameof(CurrentEnemyName));
                OnPropertyChanged(nameof(MyFieldLabel));
                OnPropertyChanged(nameof(EnemyFieldLabel));
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool CurrentPlayerReady
        {
            get
            {
                if (IsGameStarted)
                    return IsPlayer1Turn ? IsPlayer1Ready : IsPlayer2Ready;
                else
                    return !IsPlayer1Ready ? IsPlayer1Ready : IsPlayer2Ready;
            }
        }

        public bool IsDeviceLocked
        {
            get => _isDeviceLocked;
            set
            {
                _isDeviceLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

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

        public bool IsCurrentPlacementValid
        {
            get => _isCurrentPlacementValid;
            set
            {
                _isCurrentPlacementValid = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanReady));
            }
        }

        // Показывать панель кораблей только когда:
        // 1. Игра не началась
        // 2. Текущий игрок еще не готов
        public bool ShouldShowShipPanel => !IsGameStarted && !CurrentPlayerReady;

        // Показывать игровые поля когда:
        // 1. Игра началась ИЛИ
        // 2. Игрок расставляет корабли (не готов)
        public bool ShouldShowFields => IsGameStarted || !CurrentPlayerReady;

        public bool CanStartGame => !_isGameStarted && _isPlayer1Ready && _isPlayer2Ready;
        public bool CanPlaceShips => !_isGameStarted && !CurrentPlayerReady;
        public bool ShowGameControls => _isGameStarted;
        public bool ShowSetupControls => !_isGameStarted;
        public bool CanReady => !IsGameStarted && !IsDeviceLocked && IsCurrentPlacementValid;

        // Текущие доступные корабли (зависит от игрока)
        public ObservableCollection<ShipViewModel> AvailableShips { get; } = new ObservableCollection<ShipViewModel>();

        // Текущее поле игрока (видимое на экране)
        public ObservableCollection<CellViewModel> MyFieldCells { get; } = new ObservableCollection<CellViewModel>();
        // Поле противника (куда стреляем)
        public ObservableCollection<CellViewModel> EnemyFieldCells { get; } = new ObservableCollection<CellViewModel>();

        public ICommand AutoArrangeCommand { get; }
        public ICommand ReadyCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand EnemyCellClickCommand { get; }
        public ICommand SurrenderCommand { get; }
        public ICommand PassDeviceCommand { get; }
        public ICommand ResetGameCommand { get; }
        public ICommand RotateShipCommand { get; }
        public ICommand RemoveShipCommand { get; }
        public ICommand SelectShipCommand { get; }
        public ICommand ResetManualPlacementCommand { get; }
        public ICommand MyCellClickCommand { get; }
        public ICommand MyCellMouseEnterCommand { get; }
        public ICommand MyCellMouseLeaveCommand { get; }

        public HotSeatViewModel()
        {
            _shipService = new ShipPlacmentService();
            _player1Field = new GameField();
            _player2Field = new GameField();

            _player1ManualService = new ManualShipPlacementService(_player1Field, _player1Ships);
            _player2ManualService = new ManualShipPlacementService(_player2Field, _player2Ships);

            SubscribeToManualPlacementEvents();

            InitializeCells();
            InitializeAvailableShips();

            GameStatus = "Игрок 1: расставьте корабли";

            AutoArrangeCommand = new RelayCommand(AutoArrangeCurrentPlayer,
                                                  () => CanPlaceShips && !IsDeviceLocked);
            ReadyCommand = new RelayCommand(SetPlayerReady,
                                           () => CanReady);
            StartGameCommand = new RelayCommand(StartGame,
                                               () => CanStartGame && !IsDeviceLocked);
            EnemyCellClickCommand = new RelayCommand<CellViewModel>(HandleEnemyCellClick);
            SurrenderCommand = new RelayCommand(Surrender,
                                               () => IsGameStarted && !IsDeviceLocked);
            PassDeviceCommand = new RelayCommand(PassDevice);
            ResetGameCommand = new RelayCommand(ResetGame,
                                               () => !IsGameStarted && !IsDeviceLocked);
            RotateShipCommand = new RelayCommand(RotateShipExecute);
            RemoveShipCommand = new RelayCommand<ShipViewModel>(RemoveShipExecute);
            SelectShipCommand = new RelayCommand<ShipViewModel>(SelectShipExecute);
            ResetManualPlacementCommand = new RelayCommand(ResetManualPlacementExecute);
            MyCellClickCommand = new RelayCommand<CellViewModel>(MyCellClickExecute);
            MyCellMouseEnterCommand = new RelayCommand<CellViewModel>(MyCellMouseEnterExecute);
            MyCellMouseLeaveCommand = new RelayCommand<CellViewModel>(MyCellMouseLeaveExecute);
        }

        private void SubscribeToManualPlacementEvents()
        {
            _player1ManualService.PlacementValidated += OnPlayer1PlacementValidated;
            _player2ManualService.PlacementValidated += OnPlayer2PlacementValidated;

            _player1ManualService.ShipPlaced += OnShipPlaced;
            _player2ManualService.ShipPlaced += OnShipPlaced;
            _player1ManualService.ShipRemoved += OnShipRemoved;
            _player2ManualService.ShipRemoved += OnShipRemoved;

            // ДОБАВЛЯЕМ события для подсветки
            _player1ManualService.HighlightCellsRequested += OnHighlightCellsRequested;
            _player2ManualService.HighlightCellsRequested += OnHighlightCellsRequested;
            _player1ManualService.ClearHighlightRequested += OnClearHighlightRequested;
            _player2ManualService.ClearHighlightRequested += OnClearHighlightRequested;
        }

        private void OnPlayer1PlacementValidated(bool isValid)
        {
            if (!IsPlayer1Ready)
            {
                IsCurrentPlacementValid = isValid;
            }
        }

        private void OnPlayer2PlacementValidated(bool isValid)
        {
            if (!IsPlayer2Ready)
            {
                IsCurrentPlacementValid = isValid;
            }
        }

        private void OnShipPlaced(ShipViewModel ship)
        {
            UpdateFieldDisplay();
            RefreshAvailableShipsList();
        }

        private void OnShipRemoved(ShipViewModel ship)
        {
            UpdateFieldDisplay();
            RefreshAvailableShipsList();
        }

        private void OnHighlightCellsRequested(List<CellViewModel> cells)
        {
            // Очищаем предыдущую подсветку
            ClearHighlights();

            // Подсвечиваем новые клетки
            foreach (var cell in cells)
            {
                var myCell = MyFieldCells.FirstOrDefault(c => c.X == cell.X && c.Y == cell.Y);
                if (myCell != null)
                {
                    myCell.IsHighlighted = true;
                    myCell.IsShipPlacementValid = cell.IsShipPlacementValid;
                }
            }
        }

        private void OnClearHighlightRequested()
        {
            ClearHighlights();
        }

        private void InitializeCells()
        {
            MyFieldCells.Clear();
            EnemyFieldCells.Clear();

            // Изначально показываем поле Игрока 1
            UpdateFieldDisplay();
        }

        private void InitializeAvailableShips()
        {
            AvailableShips.Clear();

            if (!IsPlayer1Ready)
            {
                _player1ManualService.InitializeAvailableShips();
                foreach (var ship in _player1Ships)
                {
                    AvailableShips.Add(ship);
                }
            }
            else if (!IsPlayer2Ready)
            {
                _player2ManualService.InitializeAvailableShips();
                foreach (var ship in _player2Ships)
                {
                    AvailableShips.Add(ship);
                }
            }
        }

        private void UpdateFieldDisplay()
        {
            MyFieldCells.Clear();
            EnemyFieldCells.Clear();

            GameField myField, enemyField;

            if (!IsGameStarted)
            {
                // В фазе расстановки: показываем поле того игрока, который сейчас настраивает
                if (!IsPlayer1Ready)
                {
                    myField = _player1Field;
                    enemyField = null;
                }
                else if (!IsPlayer2Ready)
                {
                    myField = _player2Field;
                    enemyField = null;
                }
                else
                {
                    // Оба игрока готовы - показываем пустые поля
                    myField = _player1Field;
                    enemyField = null;
                }
            }
            else
            {
                // В фазе игры: показываем поле текущего игрока
                if (IsPlayer1Turn)
                {
                    myField = _player1Field;
                    enemyField = _player2Field;
                }
                else
                {
                    myField = _player2Field;
                    enemyField = _player1Field;
                }
            }

            // Заполняем "Мое поле"
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    var cellVM = new CellViewModel(x, y, myField.Cells[x, y], false);
                    MyFieldCells.Add(cellVM);
                }
            }

            // Заполняем "Поле противника" (только если игра началась и есть вражеское поле)
            if (IsGameStarted && enemyField != null)
            {
                for (int y = 0; y < 10; y++)
                {
                    for (int x = 0; x < 10; x++)
                    {
                        var cellVM = new CellViewModel(x, y, enemyField.Cells[x, y], true);
                        // Во время игры вражеские клетки должны быть кликабельны
                        cellVM.UpdateClickability(true);
                        EnemyFieldCells.Add(cellVM);
                    }
                }
            }
        }

        // Методы для ручной расстановки
        private void MyCellClickExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && ShouldShowShipPanel && !SelectedShip.IsPlaced)
            {
                if (!IsPlayer1Ready)
                {
                    if (_player1ManualService.TryPlaceShip(SelectedShip, cellVM.X, cellVM.Y))
                    {
                        UpdateFieldDisplay();
                        ClearHighlights(); // ДОБАВИТЬ ЭТО

                        SelectedShip.IsPlaced = true;
                        var nextShip = _player1ManualService.GetUnplacedShips().FirstOrDefault();
                        SelectedShip = nextShip;
                        RefreshAvailableShipsList();
                    }
                }
                else if (!IsPlayer2Ready)
                {
                    if (_player2ManualService.TryPlaceShip(SelectedShip, cellVM.X, cellVM.Y))
                    {
                        UpdateFieldDisplay();
                        ClearHighlights(); // ДОБАВИТЬ ЭТО

                        SelectedShip.IsPlaced = true;
                        var nextShip = _player2ManualService.GetUnplacedShips().FirstOrDefault();
                        SelectedShip = nextShip;
                        RefreshAvailableShipsList();
                    }
                }
            }
        }

        private void MyCellMouseEnterExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && ShouldShowShipPanel && !SelectedShip.IsPlaced)
            {
                // Очищаем предыдущую подсветку
                ClearHighlights();

                if (!IsPlayer1Ready)
                {
                    _player1ManualService.PreviewShipPlacement(SelectedShip, cellVM.X, cellVM.Y, MyFieldCells.ToList());
                }
                else if (!IsPlayer2Ready)
                {
                    _player2ManualService.PreviewShipPlacement(SelectedShip, cellVM.X, cellVM.Y, MyFieldCells.ToList());
                }
            }
        }

        private void MyCellMouseLeaveExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced)
            {
                ClearHighlights();
            }
        }

        private void RotateShipExecute()
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && ShouldShowShipPanel)
            {
                SelectedShip.Rotate();
                RefreshAvailableShipsList();
            }
        }

        private void RemoveShipExecute(ShipViewModel ship)
        {
            if (ship == null || !ship.IsPlaced || !ShouldShowShipPanel) return;

            ClearHighlights(); // ДОБАВИТЬ ЭТО

            if (!IsPlayer1Ready)
            {
                _player1ManualService.RemoveShip(ship);
            }
            else if (!IsPlayer2Ready)
            {
                _player2ManualService.RemoveShip(ship);
            }

            SelectedShip = null;
            ship.IsSelected = false;
            RefreshAvailableShipsList();
        }

        private void SelectShipExecute(ShipViewModel ship)
        {
            if (ship == null || !ShouldShowShipPanel) return;

            ClearHighlights(); // ДОБАВИТЬ ЭТО

            if (SelectedShip == ship)
            {
                SelectedShip = null;
                ship.IsSelected = false;
                RefreshAvailableShipsList();
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
                RefreshAvailableShipsList();
                return;
            }

            foreach (var availableShip in AvailableShips)
            {
                availableShip.IsSelected = false;
            }

            SelectedShip = ship;
            ship.IsSelected = true;
            RefreshAvailableShipsList();
        }

        private void ResetManualPlacementExecute()
        {
            if (!ShouldShowShipPanel) return;

            ClearHighlights(); // ДОБАВИТЬ ЭТО

            if (!IsPlayer1Ready)
            {
                _player1ManualService.ResetPlacement();
                _player1Field.ClearField();
                IsCurrentPlacementValid = false;
            }
            else if (!IsPlayer2Ready)
            {
                _player2ManualService.ResetPlacement();
                _player2Field.ClearField();
                IsCurrentPlacementValid = false;
            }

            UpdateFieldDisplay();
            InitializeAvailableShips();

            SelectedShip = null;

            GameStatus = !IsPlayer1Ready ?
                "Игрок 1: Все корабли сброшены. Выберите корабль для размещения." :
                "Игрок 2: Все корабли сброшены. Выберите корабль для размещения.";

            OnPropertyChanged(nameof(IsCurrentPlacementValid));
        }

        private void AutoArrangeCurrentPlayer()
        {
            if (!ShouldShowShipPanel) return;

            ClearHighlights(); // ДОБАВИТЬ ЭТО

            if (!IsPlayer1Ready)
            {
                _player1ManualService.ResetPlacement();
                _player1Field.ClearField();
                _shipService.PlaceShipAutomatically(_player1Field);
                UpdateFieldDisplay();
                GameStatus = "Игрок 1: корабли расставлены автоматически";
                IsCurrentPlacementValid = true;
            }
            else if (!IsPlayer2Ready)
            {
                _player2ManualService.ResetPlacement();
                _player2Field.ClearField();
                _shipService.PlaceShipAutomatically(_player2Field);
                UpdateFieldDisplay();
                GameStatus = "Игрок 2: корабли расставлены автоматически";
                IsCurrentPlacementValid = true;
            }
        }

        private void SetPlayerReady()
        {
            if (!CanReady) return;

            if (!IsPlayer1Ready)
            {
                IsPlayer1Ready = true;
                IsDeviceLocked = true;
                SelectedShip = null;
                UpdateFieldDisplay();
                GameStatus = "Игрок 1 готов! Передайте устройство Игроку 2";
            }
            else if (!IsPlayer2Ready)
            {
                IsPlayer2Ready = true;
                IsDeviceLocked = true;
                SelectedShip = null;
                UpdateFieldDisplay();
                GameStatus = "Оба игрока готовы! Нажмите 'Начать игру'";
            }
        }

        private void StartGame()
        {
            if (!CanStartGame || IsDeviceLocked) return;

            IsGameStarted = true;
            IsPlayer1Turn = true;
            IsDeviceLocked = false;
            UpdateFieldDisplay();
            GameStatus = "Игра началась! Ходит Игрок 1";
        }

        private void HandleEnemyCellClick(CellViewModel cellVM)
        {
            if (!IsGameStarted || IsDeviceLocked) return;

            GameField enemyField = IsPlayer1Turn ? _player2Field : _player1Field;

            // Проверяем, можно ли стрелять в эту клетку
            if (enemyField.Cells[cellVM.X, cellVM.Y].State != CellState.Empty &&
                enemyField.Cells[cellVM.X, cellVM.Y].State != CellState.Ship)
                return;

            var result = enemyField.Shoot(cellVM.X, cellVM.Y);
            cellVM.UpdateFromModel();

            string currentPlayer = CurrentPlayerName;
            string nextPlayer = CurrentEnemyName;

            if (result == CellState.Miss)
            {
                // Промах - блокируем устройство для передачи
                IsDeviceLocked = true;
                GameStatus = $"{currentPlayer} промахнулся!\nПередайте устройство {nextPlayer}";

                // Отключаем кликабельность вражеских клеток
                foreach (var enemyCell in EnemyFieldCells)
                {
                    enemyCell.UpdateClickability(false);
                }
            }
            else if (result == CellState.Hit || result == CellState.Sunk)
            {
                // Попадание - продолжаем ход
                if (result == CellState.Sunk)
                {
                    UpdateFieldDisplay(); // Обновляем отображение потопленного корабля
                    GameStatus = $"{currentPlayer} потопил корабль! Продолжайте ход";
                }
                else
                {
                    GameStatus = $"{currentPlayer} попал! Продолжайте ход";
                }

                // Проверяем победу
                if (enemyField.AllShipsSunk)
                {
                    IsGameStarted = false;
                    IsDeviceLocked = false;
                    GameStatus = $"{currentPlayer} победил! Все корабли потоплены";

                    var resultMsg = MessageBox.Show($"{currentPlayer} победил!\nХотите сыграть снова?",
                                                   "Игра окончена",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Question);

                    if (resultMsg == MessageBoxResult.Yes)
                    {
                        ResetGame();
                    }
                }
            }
        }

        private void PassDevice()
        {
            // Передача устройства другому игроку
            IsDeviceLocked = false;

            if (IsGameStarted)
            {
                IsPlayer1Turn = !IsPlayer1Turn;
                UpdateFieldDisplay();

                // Включаем кликабельность вражеских клеток для текущего игрока
                foreach (var enemyCell in EnemyFieldCells)
                {
                    enemyCell.UpdateClickability(true);
                }

                GameStatus = $"Ходит {CurrentPlayerName}";
            }
            else
            {
                // В фазе расстановки
                if (IsPlayer1Ready && !IsPlayer2Ready)
                {
                    // Переход от игрока 1 к игроку 2
                    InitializeAvailableShips();
                    UpdateFieldDisplay();
                    GameStatus = "Игрок 2: расставьте корабли";
                }
            }
        }

        private void Surrender()
        {
            if (!IsGameStarted) return;

            var result = MessageBox.Show("Вы уверены, что хотите сдаться?",
                                        "Сдаться",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var winner = IsPlayer1Turn ? "Игрок 2" : "Игрок 1";
                GameStatus = $"{winner} победил! Противник сдался";
                IsGameStarted = false;
                IsDeviceLocked = false;
            }
        }

        public void ResetGame()
        {
            IsGameStarted = false;
            IsPlayer1Turn = true;
            IsPlayer1Ready = false;
            IsPlayer2Ready = false;
            IsDeviceLocked = false;
            IsCurrentPlacementValid = false;

            _player1Field.ClearField();
            _player2Field.ClearField();

            _player1Ships.Clear();
            _player2Ships.Clear();
            _player1ManualService = new ManualShipPlacementService(_player1Field, _player1Ships);
            _player2ManualService = new ManualShipPlacementService(_player2Field, _player2Ships);
            SubscribeToManualPlacementEvents();

            UpdateFieldDisplay();
            InitializeAvailableShips();
            GameStatus = "Игрок 1: расставьте корабли";
        }

        private void RefreshAvailableShipsList()
        {
            var tempList = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList)
            {
                AvailableShips.Add(item);
            }
        }

        private void ClearHighlights()
        {
            foreach (var cell in MyFieldCells)
            {
                cell.ClearHighlight();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}