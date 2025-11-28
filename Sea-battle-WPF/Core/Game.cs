using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Services;

namespace Sea_battle_WPF.Core
{
    public class Game
    {
        public GameField PlayerField { get; private set; }
        public GameField EnemyField { get; private set; }
        public GamePhase CurrentPhase { get; private set; }

        private readonly ShipPlacmentService _shipPlacmentService;
        public Action<GamePhase, string> GameStateChanged;
        public event Action<bool> GameOver;

        public Game(ShipPlacmentService shipPlacementService = null)
        {
            PlayerField = new GameField();
            EnemyField = new GameField();
            _shipPlacmentService = shipPlacementService ?? new ShipPlacmentService();
            CurrentPhase = GamePhase.Setup;
        }

        public void AutoArrangePlayerShips()
        {
            _shipPlacmentService.PlaceShipAutomatically(PlayerField);
        }

        public void AutoArrangeEnemyShips()
        {
            _shipPlacmentService.PlaceShipAutomatically(EnemyField);
        }

        public void StartGame()
        {
            if (CurrentPhase != GamePhase.Setup) return;
            CurrentPhase = GamePhase.PlayerTurn;
            GameStateChanged?.Invoke(CurrentPhase, "Игра началась!");
        }
    }
}