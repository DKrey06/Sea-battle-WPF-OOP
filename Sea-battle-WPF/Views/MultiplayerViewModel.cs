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
        private ShipViewModel _selectedShip;
        private bool _isManualPlacementMode = true;
        private bool _isManualPlacementValid;

        // Добавляем новые свойства для блокировки кнопок
        public bool CanCreateRoom => IsConnected && !IsGameStarted && !IsReady;
        public bool CanJoinRoom => IsConnected && !IsGameStarted && !IsReady;

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
                OnPropertyChanged(nameof(CanCreateRoom));
                OnPropertyChanged(nameof(CanJoinRoom));
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
                OnPropertyChanged(nameof(CanCreateRoom));
                OnPropertyChanged(nameof(CanJoinRoom));
            }
        }

        public bool IsOpponentReady
        {
            get => _isOpponentReady;
            set
            {
                _isOpponentReady = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartGame));
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
                OnPropertyChanged(nameof(CanCreateRoom));
                OnPropertyChanged(nameof(CanJoinRoom));
                if (value)
                {
                    IsOpponentReady = true;
                    IsOpponentShipsPlaced = true;
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
            }
        }

        public bool IsManualPlacementValid
        {
            get => _isManualPlacementValid;
            set
            {
                _isManualPlacementValid = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartGame));
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

        public bool CanPlaceShips => IsConnected && !IsGameStarted && !IsReady;
        public bool CanCreateOrJoin => IsConnected && !IsGameStarted;
        public bool CanStartGame => IsReady && IsOpponentReady && IsOpponentShipsPlaced && !IsGameStarted;

        public ObservableCollection<ShipViewModel> AvailableShips { get; } = new ObservableCollection<ShipViewModel>();
        public ObservableCollection<CellViewModel> PlayerCells { get; } = new ObservableCollection<CellViewModel>();
        public ObservableCollection<CellViewModel> EnemyCells { get; } = new ObservableCollection<CellViewModel>();

        public ICommand ConnectCommand { get; }
        public ICommand CreateRoomCommand { get; }
        public ICommand JoinRoomCommand { get; }
        public ICommand ReadyCommand { get; }
        public ICommand CellClickCommand { get; }
        public ICommand AutoArrangeCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand PlayerCellClickCommand { get; }
        public ICommand PlayerCellMouseEnterCommand { get; }
        public ICommand PlayerCellMouseLeaveCommand { get; }
        public ICommand RotateShipCommand { get; }
        public ICommand RemoveShipCommand { get; }
        public ICommand SelectShipCommand { get; }
        public ICommand ResetManualPlacementCommand { get; }
        public ICommand SwitchToManualModeCommand { get; }
        public ICommand SwitchToAutoModeCommand { get; }

        public MultiplayerViewModel()
        {
            _networkService = new NetworkService();
            var shipService = new ShipPlacmentService();
            _game = new Game(shipService);

            var availableShipsList = new List<ShipViewModel>();
            _manualPlacementService = new ManualShipPlacementService(_game.PlayerField, availableShipsList);

            InitializeCells();
            InitializeAvailableShips(availableShipsList);
            SubscribeToManualPlacementEvents();

            SetupNetworkEvents();

            ConnectCommand = new RelayCommand(async () => await Connect());
            CreateRoomCommand = new RelayCommand(CreateRoom);
            JoinRoomCommand = new RelayCommand<string>(JoinRoom);
            ReadyCommand = new RelayCommand(SetReady);
            CellClickCommand = new RelayCommand<CellViewModel>(CellClickExecute);
            AutoArrangeCommand = new RelayCommand(AutoArrangeExecute);
            DisconnectCommand = new RelayCommand(Disconnect);
            PlayerCellClickCommand = new RelayCommand<CellViewModel>(PlayerCellClickExecute);
            PlayerCellMouseEnterCommand = new RelayCommand<CellViewModel>(PlayerCellMouseEnterExecute);
            PlayerCellMouseLeaveCommand = new RelayCommand<CellViewModel>(PlayerCellMouseLeaveExecute);
            RotateShipCommand = new RelayCommand(RotateShipExecute);
            RemoveShipCommand = new RelayCommand<ShipViewModel>(RemoveShipExecute);
            SelectShipCommand = new RelayCommand<ShipViewModel>(SelectShipExecute);
            ResetManualPlacementCommand = new RelayCommand(ResetManualPlacementExecute);
            SwitchToManualModeCommand = new RelayCommand(SwitchToManualModeExecute);
            SwitchToAutoModeCommand = new RelayCommand(SwitchToAutoModeExecute);

            GameStatus = "Нажмите 'Подключиться' для игры по сети";
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
                });
            };

            _networkService.OnRoomJoined += (roomId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RoomId = roomId;
                    IsRoomCreator = false;
                    GameStatus = $"Вы в комнате {roomId}";
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
                        IsReady = false;

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
                    OnPropertyChanged(nameof(CanCreateRoom));
                    OnPropertyChanged(nameof(CanJoinRoom));
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

                        GameStatus = shot.IsHit ? "Вы попали! Делайте следующий выстрел" : "Вы промахнулись";

                        // ПРИ ПОПАДАНИИ - оставляем клетки активными для следующего выстрела
                        if (shot.IsHit)
                        {
                            UpdateEnemyCellsClickability(true);
                        }
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
                    IsReady = false;
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
            if (IsConnected && RoomId != null && !IsReady && IsManualPlacementValid)
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
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
            }

            GameStatus = "Корабли расставлены";
            IsManualPlacementValid = true;
        }

        private void Disconnect()
        {
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
            GameStatus = "Отключено от сервера";
            OpponentStatus = "Ожидание соперника...";
            OnPropertyChanged(nameof(CanCreateRoom));
            OnPropertyChanged(nameof(CanJoinRoom));
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

        private void UpdateEnemyCellsClickability(bool clickable)
        {
            foreach (var cellVM in EnemyCells)
            {
                cellVM.UpdateClickability(clickable);
            }
        }

        // Методы для ручной расстановки кораблей
        private void PlayerCellClickExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && IsManualPlacementMode && !SelectedShip.IsPlaced && !IsReady)
            {
                if (_manualPlacementService.TryPlaceShip(SelectedShip, cellVM.X, cellVM.Y))
                {
                    UpdatePlayerCells();

                    SelectedShip.IsPlaced = true;

                    var nextShip = _manualPlacementService.GetUnplacedShips().FirstOrDefault();
                    SelectedShip = nextShip;

                    RefreshAvailableShipsList();
                }
            }
        }

        private void PlayerCellMouseEnterExecute(CellViewModel cellVM)
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && IsManualPlacementMode && !IsReady)
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

        private void RotateShipExecute()
        {
            if (SelectedShip != null && !SelectedShip.IsPlaced && IsManualPlacementMode && !IsReady)
            {
                SelectedShip.Rotate();
                OnPropertyChanged(nameof(SelectedShip));
            }
        }

        private void RemoveShipExecute(ShipViewModel ship)
        {
            if (ship == null || !ship.IsPlaced || IsReady) return;

            _manualPlacementService.RemoveShip(ship);

            SelectedShip = null;
            ship.IsSelected = false;

            RefreshAvailableShipsList();
        }

        private void SelectShipExecute(ShipViewModel ship)
        {
            if (ship == null || IsReady) return;

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
            if (IsReady) return;

            _manualPlacementService.ResetPlacement();
            _game.PlayerField.ClearField();
            UpdatePlayerCells();

            SelectedShip = null;

            foreach (var ship in AvailableShips)
            {
                ship.IsSelected = false;
                ship.IsPlaced = false;
            }

            RefreshAvailableShipsList();

            IsManualPlacementValid = false;

            GameStatus = "Все корабли сброшены. Выберите корабль для размещения.";

            OnPropertyChanged(nameof(IsManualPlacementValid));
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

        private void RefreshAvailableShipsList()
        {
            var tempList = AvailableShips.ToList();
            AvailableShips.Clear();
            foreach (var item in tempList)
            {
                AvailableShips.Add(item);
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

            RefreshAvailableShipsList();
        }

        private void OnShipRemoved(ShipViewModel ship)
        {
            UpdatePlayerCells();

            ship.IsSelected = false;
            if (SelectedShip == ship)
            {
                SelectedShip = null;
            }

            RefreshAvailableShipsList();
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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}