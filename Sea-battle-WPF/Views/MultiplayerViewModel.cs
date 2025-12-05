using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Sea_battle_WPF.Core;
using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Services;
using SeaBattle.Presentation.ViewModels;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Sea_battle_WPF.ViewModels
{
    public class MultiplayerViewModel : INotifyPropertyChanged
    {
        private readonly NetworkService _networkService;
        private readonly Game _game;
        private readonly ManualShipPlacementService _manualPlacementService;
        private string _gameStatus;
        private string _roomId;
        private string _opponentStatus = "Ожидание соперника...";
        private bool _isConnected;
        private bool _isRoomCreator;
        private bool _isReady;
        private bool _isOpponentReady;
        private bool _isOpponentShipsPlaced;
        private bool _isGameStarted;
        private string _opponentId;
        private bool _shipsSent;
        private bool _receivedPlayerJoined = false;
        private bool _isManualPlacementValid;
        private ShipViewModel _selectedShip;
        private bool _canShowRestartButton = false;

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

        public string RoomId
        {
            get => _roomId;
            set
            {
                _roomId = value;
                OnPropertyChanged();
            }
        }

        public string OpponentStatus
        {
            get => _opponentStatus;
            set
            {
                _opponentStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCreateOrJoin));
            }
        }

        public bool IsRoomCreator
        {
            get => _isRoomCreator;
            set
            {
                _isRoomCreator = value;
                OnPropertyChanged();
            }
        }

        public bool IsReady
        {
            get => _isReady;
            set
            {
                _isReady = value;
                OnPropertyChanged();
                UpdateOpponentStatus();
            }
        }

        public bool IsOpponentReady
        {
            get => _isOpponentReady;
            set
            {
                _isOpponentReady = value;
                OnPropertyChanged();
                UpdateOpponentStatus();
            }
        }

        public bool IsOpponentShipsPlaced
        {
            get => _isOpponentShipsPlaced;
            set
            {
                _isOpponentShipsPlaced = value;
                OnPropertyChanged();
                UpdateOpponentStatus();
            }
        }

        public bool IsGameStarted
        {
            get => _isGameStarted;
            set
            {
                _isGameStarted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlaceShips));
                OnPropertyChanged(nameof(CanShowShipPlacementPanel));
                OnPropertyChanged(nameof(CanShowRestartButton));

                if (value)
                {
                    CanShowRestartButton = false;
                }
                else
                {
                    CanShowRestartButton = true;
                }
            }
        }

        public bool IsManualPlacementValid
        {
            get => _isManualPlacementValid;
            set
            {
                _isManualPlacementValid = value;
                OnPropertyChanged();
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

        public bool CanShowRestartButton
        {
            get => _canShowRestartButton;
            set
            {
                _canShowRestartButton = value;
                OnPropertyChanged();
            }
        }

        public bool CanPlaceShips => IsConnected && !IsGameStarted;
        public bool CanCreateOrJoin => IsConnected && !IsGameStarted;
        public bool CanStartGame => IsReady && IsOpponentReady && IsOpponentShipsPlaced && !IsGameStarted;
        public bool CanShowShipPlacementPanel => IsConnected && !IsGameStarted;

        public ObservableCollection<ShipViewModel> AvailableShips { get; } = new ObservableCollection<ShipViewModel>();
        public ObservableCollection<CellViewModel> PlayerCells { get; } = new ObservableCollection<CellViewModel>();
        public ObservableCollection<CellViewModel> EnemyCells { get; } = new ObservableCollection<CellViewModel>();

        // Команды управления сетью
        public ICommand ConnectCommand { get; private set; }
        public ICommand CreateRoomCommand { get; private set; }
        public ICommand JoinRoomCommand { get; private set; }
        public ICommand ReadyCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }

        // Команды управления игрой
        public ICommand CellClickCommand { get; private set; }
        public ICommand AutoArrangeCommand { get; private set; }
        public ICommand SurrenderCommand { get; private set; }
        public ICommand StartNewGameCommand { get; private set; }
        public ICommand SelectShipCommand { get; private set; }
        public ICommand RotateShipCommand { get; private set; }
        public ICommand PlayerCellClickCommand { get; private set; }
        public ICommand PlayerCellMouseEnterCommand { get; private set; }
        public ICommand PlayerCellMouseLeaveCommand { get; private set; }

        // Команды ручной расстановки
        public ICommand RemoveShipCommand { get; private set; }
        public ICommand ResetManualPlacementCommand { get; private set; }

        public MultiplayerViewModel()
        {
            _networkService = new NetworkService();
            var shipService = new ShipPlacmentService();
            _game = new Game(shipService);

            var availableShipsList = new List<ShipViewModel>();
            _manualPlacementService = new ManualShipPlacementService(_game.PlayerField, availableShipsList);

            // Подписка на события ручной расстановки
            SubscribeToManualPlacementEvents();

            InitializeCells();
            InitializeAvailableShips(availableShipsList);

            SetupNetworkEvents();

            // Инициализация команд
            InitializeCommands();

            GameStatus = "Нажмите 'Подключиться' для игры по сети";
        }

        private void InitializeCommands()
        {
            // Команды сети
            ConnectCommand = new RelayCommand(async () => await Connect());
            CreateRoomCommand = new RelayCommand(CreateRoom);
            JoinRoomCommand = new RelayCommand<string>(JoinRoom);
            ReadyCommand = new RelayCommand(SetReady, () => IsConnected && RoomId != null && !IsReady && _game.PlayerField.Ships.Count == 10);
            DisconnectCommand = new RelayCommand(Disconnect);

            // Команды игры
            CellClickCommand = new RelayCommand<CellViewModel>(CellClickExecute);
            AutoArrangeCommand = new RelayCommand(AutoArrangeExecute, () => CanPlaceShips);
            SurrenderCommand = new RelayCommand(SurrenderExecute, () => IsGameStarted);
            StartNewGameCommand = new RelayCommand(StartNewGameExecute, () => CanShowRestartButton);
            SelectShipCommand = new RelayCommand<ShipViewModel>(SelectShipExecute);
            RotateShipCommand = new RelayCommand(RotateShipExecute);
            PlayerCellClickCommand = new RelayCommand<CellViewModel>(PlayerCellClickExecute);
            PlayerCellMouseEnterCommand = new RelayCommand<CellViewModel>(PlayerCellMouseEnterExecute);
            PlayerCellMouseLeaveCommand = new RelayCommand<CellViewModel>(PlayerCellMouseLeaveExecute);

            // Команды ручной расстановки
            RemoveShipCommand = new RelayCommand<ShipViewModel>(RemoveShipExecute);
            ResetManualPlacementCommand = new RelayCommand(ResetManualPlacementExecute);
        }

        private void SubscribeToManualPlacementEvents()
        {
            _manualPlacementService.ShipPlaced += OnShipPlaced;
            _manualPlacementService.ShipRemoved += OnShipRemoved;
            _manualPlacementService.PlacementValidated += OnPlacementValidated;
            _manualPlacementService.HighlightCellsRequested += OnHighlightCellsRequested;
            _manualPlacementService.ClearHighlightRequested += OnClearHighlightRequested;
        }

        private void SetupNetworkEvents()
        {
            _networkService.OnRoomCreated += (roomId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RoomId = roomId;
                    IsRoomCreator = true;
                    GameStatus = $"Комната {roomId} создана. Ждите соперника";
                    CanShowRestartButton = false;
                });
            };

            _networkService.OnRoomJoined += (roomId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RoomId = roomId;
                    IsRoomCreator = false;
                    GameStatus = $"Вы в комнате {roomId}";
                    CanShowRestartButton = false;
                });
            };

            _networkService.OnPlayerJoined += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _opponentId = playerId;
                    _receivedPlayerJoined = true;
                    OpponentStatus = "Соперник присоединился";
                    GameStatus = "Соперник найден. Расставьте корабли и нажмите 'Готов'";
                });
            };

            // Обработка готовности противника
            _networkService.OnPlayerReady += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_opponentId == null)
                    {
                        _opponentId = playerId;
                    }

                    if (playerId != _networkService.PlayerId &&
                        (_opponentId == null || playerId == _opponentId))
                    {
                        IsOpponentReady = true;
                        UpdateOpponentStatus();

                        if (IsRoomCreator && !_receivedPlayerJoined && _opponentId == null)
                        {
                            _opponentId = playerId;
                            _receivedPlayerJoined = true;
                            OpponentStatus = "Соперник присоединился и готов";
                        }
                    }
                });
            };

            // Обработка расстановки кораблей противником
            _networkService.OnShipsPlacedNotify += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (playerId != _networkService.PlayerId &&
                        (_opponentId == null || playerId == _opponentId))
                    {
                        if (_opponentId == null)
                        {
                            _opponentId = playerId;
                        }

                        IsOpponentShipsPlaced = true;
                        UpdateOpponentStatus();
                    }
                });
            };

            // Обработка отключения игрока
            _networkService.OnPlayerDisconnected += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_opponentId == null || playerId == _opponentId)
                    {
                        OpponentStatus = "Соперник отключился";
                        GameStatus = "Соперник покинул игру";
                        IsOpponentReady = false;
                        IsOpponentShipsPlaced = false;
                        IsGameStarted = false;
                        _shipsSent = false;
                        CanShowRestartButton = true;

                        MessageBox.Show("Соперник отключился от игры.", "Игра прервана",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            };

            _networkService.OnGameStarted += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsGameStarted = true;
                    IsOpponentReady = true;
                    IsOpponentShipsPlaced = true;
                    CanShowRestartButton = false;
                    UpdateOpponentStatus();

                    if (_networkService.IsMyTurn)
                    {
                        GameStatus = "Игра началась! Ваш ход";
                        UpdateEnemyCellsClickability(true);
                    }
                    else
                    {
                        GameStatus = "Игра началась! Ход соперника";
                        UpdateEnemyCellsClickability(false);
                    }
                });
            };

            _networkService.OnTurnChanged += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_networkService.IsMyTurn)
                    {
                        GameStatus = "Ваш ход!";
                        UpdateEnemyCellsClickability(true);
                    }
                    else
                    {
                        GameStatus = "Ход соперника...";
                        UpdateEnemyCellsClickability(false);
                    }
                });
            };

            _networkService.OnShotResult += (shot) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (shot.PlayerId == _networkService.PlayerId)
                    {
                        // Наш выстрел
                        var cell = _game.EnemyField.Cells[shot.X, shot.Y];
                        cell.State = shot.IsHit ? CellState.Hit : CellState.Miss;

                        var cellVM = EnemyCells.First(c => c.X == shot.X && c.Y == shot.Y);
                        cellVM.UpdateFromModel();

                        GameStatus = shot.IsHit ? "Вы попали!" : "Вы промахнулись";
                    }
                    else
                    {
                        // Выстрел противника
                        var cell = _game.PlayerField.Cells[shot.X, shot.Y];
                        if (shot.IsHit)
                        {
                            _game.PlayerField.Shoot(shot.X, shot.Y);
                            GameStatus = "Соперник попал!";
                        }
                        else
                        {
                            cell.State = CellState.Miss;
                            GameStatus = "Соперник промахнулся";
                        }

                        var cellVM = PlayerCells.First(c => c.X == shot.X && c.Y == shot.Y);
                        cellVM.UpdateFromModel();

                        // Проверка проигрыша
                        if (_game.PlayerField.AllShipsSunk)
                        {
                            GameStatus = "Вы проиграли!";
                            CanShowRestartButton = true;
                            MessageBox.Show("Все ваши корабли потоплены!", "Поражение",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            UpdateEnemyCellsClickability(false);
                        }
                    }
                });
            };

            _networkService.OnGameOver += (winnerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (winnerId == _networkService.PlayerId)
                    {
                        GameStatus = "Вы победили!";
                        MessageBox.Show("Поздравляем! Вы выиграли!", "Победа",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        GameStatus = "Вы проиграли!";
                        MessageBox.Show("Все ваши корабли потоплены!", "Поражение",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    UpdateEnemyCellsClickability(false);
                    IsGameStarted = false;
                    _shipsSent = false;
                    CanShowRestartButton = true;
                });
            };
        }

        private void UpdateOpponentStatus()
        {
            if (IsOpponentReady && IsOpponentShipsPlaced)
            {
                OpponentStatus = "Соперник готов";
                if (IsReady && _shipsSent)
                {
                    GameStatus = "Оба игрока готовы! Игра скоро начнется...";
                }
                else if (IsReady)
                {
                    GameStatus = "Вы готовы. Ждем, пока соперник расставит корабли...";
                }
            }
            else if (IsOpponentReady)
            {
                OpponentStatus = "Соперник готов";
                if (!IsReady)
                {
                    GameStatus = "Соперник готов. Расставьте корабли и нажмите 'Готов'";
                }
                else
                {
                    GameStatus = "Вы готовы. Ждем, пока соперник расставит корабли...";
                }
            }
            else if (_opponentId != null)
            {
                OpponentStatus = "Соперник присоединился";
                GameStatus = "Соперник найден. Расставьте корабли и нажмите 'Готов'";
            }
            else
            {
                OpponentStatus = "Ожидание соперника...";
            }
        }

        private async Task Connect()
        {
            if (await _networkService.Connect())
            {
                IsConnected = true;
                GameStatus = "Подключено. Создайте комнату или введите ID";
                CanShowRestartButton = false;
            }
        }

        private void CreateRoom()
        {
            if (IsConnected)
            {
                _networkService.CreateRoom();
            }
        }

        private void JoinRoom(string roomId)
        {
            if (IsConnected && !string.IsNullOrEmpty(roomId))
            {
                _networkService.JoinRoom(roomId);
            }
        }

        private void SetReady()
        {
            if (IsConnected && RoomId != null && !IsReady && _game.PlayerField.Ships.Count == 10)
            {
                // Сначала отправляем корабли
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
                _shipsSent = true;

                // Затем сообщаем о готовности
                _networkService.SetReady();
                IsReady = true;

                UpdateOpponentStatus();
            }
        }

        private List<ShipPlacement> ConvertShipsToNetworkFormat()
        {
            var placements = new List<ShipPlacement>();

            foreach (var ship in _game.PlayerField.Ships)
            {
                var placement = new ShipPlacement
                {
                    Size = ship.Size,
                    X = ship.Cells[0].X,
                    Y = ship.Cells[0].Y,
                    IsVertical = ship.Direction == ShipDirection.Vertical
                };

                foreach (var cell in ship.Cells)
                {
                    placement.Cells.Add(new Core.Services.Cell { X = cell.X, Y = cell.Y });
                }

                placements.Add(placement);
            }

            return placements;
        }

        private void CellClickExecute(CellViewModel cellVM)
        {
            if (IsGameStarted && _networkService.IsMyTurn &&
                cellVM.IsClickable && _game.EnemyField.Cells[cellVM.X, cellVM.Y].State == CellState.Empty)
            {
                _networkService.SendShot(cellVM.X, cellVM.Y);
                UpdateEnemyCellsClickability(false);
            }
        }

        private void AutoArrangeExecute()
        {
            _manualPlacementService.ResetPlacement();
            _game.PlayerField.ClearField();
            _game.AutoArrangePlayerShips();
            UpdatePlayerCells();

            if (_shipsSent)
            {
                // Если уже отправляли корабли, отправляем заново
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
            }

            GameStatus = "Корабли расставлены";
        }

        private void Disconnect()
        {
            _networkService.Disconnect();
            ResetGameState();
            GameStatus = "Отключено от сервера";
            OpponentStatus = "Ожидание соперника...";
        }

        private void StartNewGameExecute()
        {
            ResetGameState();
            GameStatus = "Нажмите 'Подключиться' для игры по сети";
        }

        private void ResetGameState()
        {
            // Сброс сетевого состояния
            _networkService.Disconnect();
            IsConnected = false;
            IsGameStarted = false;
            IsReady = false;
            IsOpponentReady = false;
            IsOpponentShipsPlaced = false;
            _opponentId = null;
            _receivedPlayerJoined = false;
            _shipsSent = false;
            RoomId = "";
            CanShowRestartButton = false;

            // Сброс игрового состояния
            _game.ResetGame();
            _manualPlacementService.ResetPlacement();

            // Сброс кораблей
            foreach (var ship in AvailableShips)
            {
                ship.IsSelected = false;
                ship.IsPlaced = false;
            }

            SelectedShip = null;

            // Обновление ячеек
            UpdatePlayerCells();
            UpdateEnemyCells();
            UpdateEnemyCellsClickability(false);

            // Обновление свойств
            OnPropertyChanged(nameof(CanPlaceShips));
            OnPropertyChanged(nameof(CanCreateOrJoin));
            OnPropertyChanged(nameof(CanStartGame));
            OnPropertyChanged(nameof(CanShowShipPlacementPanel));
        }

        private void SurrenderExecute()
        {
            if (!IsGameStarted) return;

            var result = MessageBox.Show("Вы уверены, что хотите сдаться?", "Сдаться",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                GameStatus = "Вы сдались";
                CanShowRestartButton = true;
                IsGameStarted = false;
                UpdateEnemyCellsClickability(false);

                MessageBox.Show("Вы сдались. Нажмите 'Начать заново' для новой игры.", "Игра окончена",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SelectShipExecute(ShipViewModel ship)
        {
            if (ship == null) return;

            if (SelectedShip == ship)
            {
                SelectedShip = null;
                ship.IsSelected = false;
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
                return;
            }

            foreach (var availableShip in AvailableShips)
            {
                availableShip.IsSelected = false;
            }

            SelectedShip = ship;
            ship.IsSelected = true;
        }

        private void RotateShipExecute()
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced)
            {
                SelectedShip.Rotate();
                OnPropertyChanged(nameof(SelectedShip));
            }
        }

        private void PlayerCellClickExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && CanPlaceShips)
            {
                if (_manualPlacementService.TryPlaceShip(SelectedShip, cellVM.X, cellVM.Y))
                {
                    UpdatePlayerCells();

                    SelectedShip.IsPlaced = true;

                    var nextShip = _manualPlacementService.GetUnplacedShips().FirstOrDefault();
                    SelectedShip = nextShip;
                }
            }
        }

        private void PlayerCellMouseEnterExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && CanPlaceShips)
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

        private void RemoveShipExecute(ShipViewModel ship)
        {
            if (ship == null || !ship.IsPlaced) return;

            _manualPlacementService.RemoveShip(ship);
            SelectedShip = null;
            ship.IsSelected = false;
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

            IsManualPlacementValid = false;
            GameStatus = "Все корабли сброшены. Выберите корабль для размещения.";
        }

        // Обработчики событий ручной расстановки
        private void OnShipPlaced(ShipViewModel ship)
        {
            UpdatePlayerCells();

            var shipInList = AvailableShips.FirstOrDefault(s => s == ship);
            if (shipInList != null)
            {
                shipInList.IsPlaced = true;
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
        }

        private void OnPlacementValidated(bool isValid)
        {
            IsManualPlacementValid = isValid;
            OnPropertyChanged(nameof(IsManualPlacementValid));
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

        private void InitializeCells()
        {
            PlayerCells.Clear();
            EnemyCells.Clear();

            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    PlayerCells.Add(new CellViewModel(x, y, _game.PlayerField.Cells[x, y], false));
                    EnemyCells.Add(new CellViewModel(x, y, _game.EnemyField.Cells[x, y], true));
                }
            }
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

        private void UpdateEnemyCellsClickability(bool clickable)
        {
            if (_game.EnemyField.AllShipsSunk) return;

            foreach (var cellVM in EnemyCells)
            {
                cellVM.UpdateClickability(clickable);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}