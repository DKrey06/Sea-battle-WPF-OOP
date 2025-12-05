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

                if (value)
                {
                    CanShowRestartButton = false;
                }
                else if (_opponentId != null)
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

                foreach (var ship in AvailableShips)
                {
                    ship.IsSelected = ship == value;
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

        public ObservableCollection<ShipViewModel> AvailableShips { get; } = new ObservableCollection<ShipViewModel>();
        public ObservableCollection<CellViewModel> PlayerCells { get; } = new ObservableCollection<CellViewModel>();
        public ObservableCollection<CellViewModel> EnemyCells { get; } = new ObservableCollection<CellViewModel>();

        public ICommand ConnectCommand { get; private set; }
        public ICommand CreateRoomCommand { get; private set; }
        public ICommand JoinRoomCommand { get; private set; }
        public ICommand ReadyCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }
        public ICommand CellClickCommand { get; private set; }
        public ICommand AutoArrangeCommand { get; private set; }
        public ICommand SurrenderCommand { get; private set; }
        public ICommand StartNewGameCommand { get; private set; }
        public ICommand SelectShipCommand { get; private set; }
        public ICommand RotateShipCommand { get; private set; }
        public ICommand PlayerCellClickCommand { get; private set; }
        public ICommand RemoveShipCommand { get; private set; }
        public ICommand ResetManualPlacementCommand { get; private set; }

        public MultiplayerViewModel()
        {
            _networkService = new NetworkService();
            var shipService = new ShipPlacmentService();
            _game = new Game(shipService);

            var availableShipsList = new List<ShipViewModel>();
            _manualPlacementService = new ManualShipPlacementService(_game.PlayerField, availableShipsList);

            InitializeCells();
            InitializeAvailableShips(availableShipsList);
            SetupNetworkEvents();
            InitializeCommands();

            GameStatus = "Нажмите 'Подключиться' для игры по сети";
        }

        private void InitializeCommands()
        {
            ConnectCommand = new RelayCommand(async () => await Connect());
            CreateRoomCommand = new RelayCommand(CreateRoom);
            JoinRoomCommand = new RelayCommand<string>(JoinRoom);
            ReadyCommand = new RelayCommand(SetReady);
            DisconnectCommand = new RelayCommand(Disconnect);
            CellClickCommand = new RelayCommand<CellViewModel>(CellClickExecute);
            AutoArrangeCommand = new RelayCommand(AutoArrangeExecute);
            SurrenderCommand = new RelayCommand(SurrenderExecute);
            StartNewGameCommand = new RelayCommand(StartNewGameExecute);
            SelectShipCommand = new RelayCommand<ShipViewModel>(SelectShipExecute);
            RotateShipCommand = new RelayCommand(RotateShipExecute);
            PlayerCellClickCommand = new RelayCommand<CellViewModel>(PlayerCellClickExecute);
            RemoveShipCommand = new RelayCommand<ShipViewModel>(RemoveShipExecute);
            ResetManualPlacementCommand = new RelayCommand(ResetManualPlacementExecute);
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
                    if (_opponentId == null) _opponentId = playerId;

                    if (playerId != _networkService.PlayerId && playerId == _opponentId)
                    {
                        IsOpponentReady = true;
                        UpdateOpponentStatus();
                    }
                });
            };

            _networkService.OnShipsPlacedNotify += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (playerId != _networkService.PlayerId && playerId == _opponentId)
                    {
                        IsOpponentShipsPlaced = true;
                        UpdateOpponentStatus();
                    }
                });
            };

            _networkService.OnPlayerDisconnected += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (playerId == _opponentId)
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
            }
            else if (IsOpponentReady)
            {
                OpponentStatus = "Соперник готов";
            }
            else if (_opponentId != null)
            {
                OpponentStatus = "Соперник присоединился";
            }
            else
            {
                OpponentStatus = "Ожидание соперника...";
            }
        }

        private async Task Connect()
        {
            try
            {
                if (await _networkService.Connect())
                {
                    IsConnected = true;
                    GameStatus = "Подключено. Создайте комнату или введите ID";
                }
                else
                {
                    MessageBox.Show("Не удалось подключиться к серверу", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateRoom()
        {
            if (IsConnected) _networkService.CreateRoom();
        }

        private void JoinRoom(string roomId)
        {
            if (IsConnected && !string.IsNullOrEmpty(roomId))
                _networkService.JoinRoom(roomId);
        }

        private void SetReady()
        {
            if (IsConnected && RoomId != null && !IsReady && _game.PlayerField.Ships.Count == 10)
            {
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
                _shipsSent = true;
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
            if (!CanPlaceShips) return;

            _manualPlacementService.ResetPlacement();
            _game.PlayerField.ClearField();
            _game.AutoArrangePlayerShips();
            UpdatePlayerCells();

            foreach (var ship in AvailableShips)
                ship.IsPlaced = true;

            IsManualPlacementValid = true;

            if (_shipsSent)
            {
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
            }

            GameStatus = "Корабли расставлены автоматически";
        }

        private void Disconnect()
        {
            try
            {
                _networkService.Disconnect();
            }
            catch { }
            finally
            {
                ResetGameState();
                GameStatus = "Отключено от сервера";
                OpponentStatus = "Ожидание соперника...";
            }
        }

        private void StartNewGameExecute()
        {
            var result = MessageBox.Show("Начать новую игру? Текущая игра будет сброшена.",
                "Новая игра", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ResetGameState();
                GameStatus = "Нажмите 'Подключиться' для игры по сети";
            }
        }

        private void ResetGameState()
        {
            try
            {
                _networkService.Disconnect();
                IsConnected = false;
                IsGameStarted = false;
                IsReady = false;
                IsOpponentReady = false;
                IsOpponentShipsPlaced = false;
                IsManualPlacementValid = false;
                _opponentId = null;
                _receivedPlayerJoined = false;
                _shipsSent = false;
                RoomId = "";
                CanShowRestartButton = false;

                _game.ResetGame();
                _manualPlacementService.ResetPlacement();

                foreach (var ship in AvailableShips)
                {
                    ship.IsSelected = false;
                    ship.IsPlaced = false;
                }

                SelectedShip = null;
                UpdatePlayerCells();
                UpdateEnemyCells();
                UpdateEnemyCellsClickability(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сброса игры: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                return;
            }

            SelectedShip = ship;
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
                    IsManualPlacementValid = AvailableShips.All(s => s.IsPlaced);

                    var nextShip = AvailableShips.FirstOrDefault(s => !s.IsPlaced);
                    SelectedShip = nextShip;
                }
            }
        }

        private void RemoveShipExecute(ShipViewModel ship)
        {
            if (ship == null || !ship.IsPlaced) return;

            _manualPlacementService.RemoveShip(ship);
            UpdatePlayerCells();
            ship.IsPlaced = false;
            SelectedShip = null;
            IsManualPlacementValid = AvailableShips.All(s => s.IsPlaced);
        }

        private void ResetManualPlacementExecute()
        {
            try
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
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сброса расстановки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (_game.EnemyField.AllShipsSunk) clickable = false;

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