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
using System.Linq;
using System.Collections.Generic;

namespace Sea_battle_WPF.ViewModels
{
    public class HotSeatViewModel : INotifyPropertyChanged
    {
        private GameField _player1Field;
        private GameField _player2Field;
        private readonly ShipPlacmentService _shipService;
        private string _gameStatus;
        private bool _isGameStarted;
        private bool _isPlayer1Turn = true;
        private bool _isPlayer1Ready = false;
        private bool _isPlayer2Ready = false;
        private bool _isDeviceLocked = false;

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
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool IsPlayer1Turn
        {
            get => _isGameStarted && _isPlayer1Turn;
            private set
            {
                _isPlayer1Turn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPlayer2Turn));
                OnPropertyChanged(nameof(CurrentPlayerName));
                OnPropertyChanged(nameof(CurrentEnemyName));
                OnPropertyChanged(nameof(MyFieldLabel));
                OnPropertyChanged(nameof(EnemyFieldLabel));
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool IsPlayer2Turn => _isGameStarted && !_isPlayer1Turn;

        public string CurrentPlayerName => IsPlayer1Turn ? "Игрок 1" : "Игрок 2";
        public string CurrentEnemyName => IsPlayer1Turn ? "Игрок 2" : "Игрок 1";
        public string MyFieldLabel => "Мое поле";
        public string EnemyFieldLabel => "Поле противника";

        public bool IsPlayer1Ready
        {
            get => _isPlayer1Ready;
            set
            {
                _isPlayer1Ready = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartGame));
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
                OnPropertyChanged(nameof(ShouldShowFields));
            }
        }

        public bool IsDeviceLocked
        {
            get => _isDeviceLocked;
            set
            {
                _isDeviceLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeviceLockVisibility));
                OnPropertyChanged(nameof(FieldVisibility));
            }
        }

        // Показывать поля только когда:
        // 1. Игрок расставляет корабли (еще не готов)
        // 2. Во время игры
        // НЕ показывать когда оба игрока готовы (но игра еще не началась)
        public bool ShouldShowFields
        {
            get
            {
                if (IsGameStarted) return true; // Во время игры показываем

                // Во время расстановки показываем только если игрок еще не готов
                if (!IsPlayer1Ready) return true; // Игрок 1 расставляет
                if (IsPlayer1Ready && !IsPlayer2Ready) return true; // Игрок 2 расставляет

                return false; // Оба готовы - не показываем
            }
        }

        public Visibility DeviceLockVisibility => IsDeviceLocked ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FieldVisibility => ShouldShowFields ? Visibility.Visible : Visibility.Collapsed;

        public bool CanStartGame => !_isGameStarted &&
                                   _isPlayer1Ready &&
                                   _isPlayer2Ready;

        public bool CanPlaceShips => !_isGameStarted;
        public bool ShowGameControls => _isGameStarted;
        public bool ShowSetupControls => !_isGameStarted;

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

        public HotSeatViewModel()
        {
            _shipService = new ShipPlacmentService();
            _player1Field = new GameField();
            _player2Field = new GameField();

            InitializeCells();

            GameStatus = "Игрок 1: расставьте корабли";

            AutoArrangeCommand = new RelayCommand(AutoArrangeCurrentPlayer,
                                                  () => CanPlaceShips && !IsDeviceLocked);
            ReadyCommand = new RelayCommand(SetPlayerReady,
                                           () => CanPlaceShips && !IsDeviceLocked);
            StartGameCommand = new RelayCommand(StartGame,
                                               () => CanStartGame && !IsDeviceLocked);
            EnemyCellClickCommand = new RelayCommand<CellViewModel>(HandleEnemyCellClick);
            SurrenderCommand = new RelayCommand(Surrender,
                                               () => IsGameStarted && !IsDeviceLocked);
            PassDeviceCommand = new RelayCommand(PassDevice);
            ResetGameCommand = new RelayCommand(ResetGame,
                                               () => !IsGameStarted && !IsDeviceLocked);
        }

        private void InitializeCells()
        {
            MyFieldCells.Clear();
            EnemyFieldCells.Clear();

            // Изначально показываем поле Игрока 1
            UpdateFieldDisplay();
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
                    enemyField = null; // Враг не нужен при расстановке
                }
                else if (!IsPlayer2Ready)
                {
                    myField = _player2Field;
                    enemyField = null; // Враг не нужен при расстановке
                }
                else
                {
                    // Оба игрока готовы - не показываем поля
                    return;
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
                    MyFieldCells.Add(new CellViewModel(x, y, myField.Cells[x, y], false));
                }
            }

            // Заполняем "Поле противника" (только если игра началась и есть вражеское поле)
            if (IsGameStarted && enemyField != null)
            {
                for (int y = 0; y < 10; y++)
                {
                    for (int x = 0; x < 10; x++)
                    {
                        EnemyFieldCells.Add(new CellViewModel(x, y, enemyField.Cells[x, y], true));
                    }
                }
            }
        }

        private void AutoArrangeCurrentPlayer()
        {
            if (!IsPlayer1Ready)
            {
                // Расстановка для Игрока 1
                _player1Field.ClearField();
                _shipService.PlaceShipAutomatically(_player1Field);
                UpdateFieldDisplay();
                GameStatus = "Игрок 1: корабли расставлены. Нажмите 'Готов'";
            }
            else if (!IsPlayer2Ready)
            {
                // Расстановка для Игрока 2
                _player2Field.ClearField();
                _shipService.PlaceShipAutomatically(_player2Field);
                UpdateFieldDisplay();
                GameStatus = "Игрок 2: корабли расставлены. Нажмите 'Готов'";
            }
        }

        private void SetPlayerReady()
        {
            if (!IsPlayer1Ready)
            {
                IsPlayer1Ready = true;
                IsDeviceLocked = true;
                UpdateFieldDisplay(); // Обновляем отображение (теперь поля скрыты)
                GameStatus = "Игрок 1 готов! Передайте устройство Игроку 2";
            }
            else if (!IsPlayer2Ready)
            {
                IsPlayer2Ready = true;
                IsDeviceLocked = true;
                UpdateFieldDisplay(); // Поля скрыты
                GameStatus = "Оба игрока готовы! Нажмите 'Начать игру'";
            }
        }

        private void StartGame()
        {
            if (!CanStartGame) return;

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
            var result = enemyField.Shoot(cellVM.X, cellVM.Y);
            cellVM.UpdateFromModel();

            string currentPlayer = CurrentPlayerName;
            string nextPlayer = CurrentEnemyName;

            if (result == CellState.Miss)
            {
                // Промах - блокируем устройство для передачи
                IsDeviceLocked = true;
                GameStatus = $"{currentPlayer} промахнулся!\nПередайте устройство {nextPlayer}";
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
                GameStatus = $"Ходит {CurrentPlayerName}";
            }
            else
            {
                // В фазе расстановки
                UpdateFieldDisplay();

                if (IsPlayer1Ready && !IsPlayer2Ready)
                {
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

            _player1Field.ClearField();
            _player2Field.ClearField();

            UpdateFieldDisplay();
            GameStatus = "Игрок 1: расставьте корабли";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}