﻿using System;
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
        private bool _isOpponentReady = false;
        private bool _isOpponentShipsPlaced = false;
        private bool _isGameStarted;
        private string _opponentId;
        private bool _shipsSent;
        private bool _receivedPlayerJoined = false;
        private bool _isSelfReady = false; // Добавляем отдельный флаг для своей готовности

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
                if (value)
                {
                    // При начале игры обновляем статусы
                    IsOpponentReady = true;
                    IsOpponentShipsPlaced = true;
                }
            }
        }

        public bool CanPlaceShips => IsConnected && !IsGameStarted;
        public bool CanCreateOrJoin => IsConnected && !IsGameStarted;
        public bool CanStartGame => IsReady && _shipsSent && IsOpponentReady && IsOpponentShipsPlaced && !IsGameStarted;

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

            ConnectCommand = new RelayCommand(async () => await Connect());
            CreateRoomCommand = new RelayCommand(CreateRoom);
            JoinRoomCommand = new RelayCommand<string>(JoinRoom);
            ReadyCommand = new RelayCommand(SetReady);
            CellClickCommand = new RelayCommand<CellViewModel>(CellClickExecute);
            AutoArrangeCommand = new RelayCommand(AutoArrangeExecute);
            DisconnectCommand = new RelayCommand(Disconnect);

            GameStatus = "Нажмите 'Подключиться' для игры по сети";
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
                    // Только если это не наш собственный ID
                    if (playerId != _networkService.PlayerId)
                    {
                        _opponentId = playerId;
                        _receivedPlayerJoined = true;
                        OpponentStatus = "Соперник присоединился";
                        GameStatus = "Соперник найден. Расставьте корабли и нажмите 'Готов'";
                    }
                });
            };

            // Обработка готовности противника
            _networkService.OnPlayerReady += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Проверяем, что это не наш собственный ID
                    if (playerId != _networkService.PlayerId)
                    {
                        // Если мы еще не знаем ID противника, устанавливаем его
                        if (_opponentId == null)
                        {
                            _opponentId = playerId;
                            _receivedPlayerJoined = true;
                        }

                        // Обновляем статус только если это ID нашего противника
                        if (playerId == _opponentId)
                        {
                            IsOpponentReady = true;
                            UpdateOpponentStatus();
                        }
                    }
                });
            };

            // Обработка расстановки кораблей противником
            _networkService.OnShipsPlacedNotify += (playerId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Проверяем, что это не наш собственный ID
                    if (playerId != _networkService.PlayerId)
                    {
                        // Если мы еще не знаем ID противника, устанавливаем его
                        if (_opponentId == null)
                        {
                            _opponentId = playerId;
                        }

                        // Обновляем статус только если это ID нашего противника
                        if (playerId == _opponentId)
                        {
                            IsOpponentShipsPlaced = true;
                            UpdateOpponentStatus();
                        }
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
                        _isSelfReady = false;

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
                    _isSelfReady = false;
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
                    GameStatus = "Вы нажали 'Готов', но нужно расставить корабли";
                }
                else if (_shipsSent)
                {
                    GameStatus = "Вы расставили корабли, но нужно нажать 'Готов'";
                }
            }
            else if (IsOpponentReady)
            {
                OpponentStatus = "Соперник нажал 'Готов'";

                if (!IsReady && !_shipsSent)
                {
                    GameStatus = "Соперник готов. Расставьте корабли и нажмите 'Готов'";
                }
                else if (_shipsSent && !IsReady)
                {
                    GameStatus = "Корабли расставлены. Нажмите 'Готов'";
                }
                else if (IsReady && !_shipsSent)
                {
                    GameStatus = "Вы нажали 'Готов'. Расставьте корабли";
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

                if (IsRoomCreator)
                {
                    GameStatus = $"Комната {RoomId} создана. Ждите соперника";
                }
                else if (RoomId != null)
                {
                    GameStatus = $"Вы в комнате {RoomId}. Ждите создателя";
                }
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
            if (IsConnected && RoomId != null && !_isSelfReady)
            {
                // Сначала отправляем корабли
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
                _shipsSent = true;

                // Затем сообщаем о готовности
                _networkService.SetReady();
                IsReady = true;
                _isSelfReady = true;

                UpdateOpponentStatus();
            }
            else if (IsConnected && RoomId != null && _isSelfReady)
            {
                // Уже готовы, показываем сообщение
                GameStatus = "Вы уже готовы. Ждем соперника...";
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

            if (_isSelfReady && !_shipsSent)
            {
                // Если уже нажали "Готов", но еще не отправляли корабли
                var ships = ConvertShipsToNetworkFormat();
                _networkService.SendShips(ships);
                _shipsSent = true;
            }
            else if (_shipsSent)
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
            IsConnected = false;
            IsGameStarted = false;
            IsReady = false;
            IsOpponentReady = false;
            IsOpponentShipsPlaced = false;
            _opponentId = null;
            _receivedPlayerJoined = false;
            _shipsSent = false;
            _isSelfReady = false;
            RoomId = "";
            GameStatus = "Отключено от сервера";
            OpponentStatus = "Ожидание соперника...";
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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}