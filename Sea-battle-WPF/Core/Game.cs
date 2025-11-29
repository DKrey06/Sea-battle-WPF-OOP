using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using Sea_battle_WPF.Core.Services;
using Sea_battle_WPF.Core.GameAI;

namespace Sea_battle_WPF.Core
{
    public class Game
    {
        public GameField PlayerField { get; private set; }
        public GameField EnemyField { get; private set; }
        public GamePhase CurrentPhase { get; set; }
        public bool LastAIMoveWasHit { get; private set; }

        private readonly ShipPlacmentService _shipPlacmentService;
        private AIBase _enemyAI;
        public Action<GamePhase, string> GameStateChanged;
        public event Action<bool> GameOver;

        public Game(ShipPlacmentService shipPlacementService = null)
        {
            PlayerField = new GameField();
            EnemyField = new GameField();
            _shipPlacmentService = shipPlacementService ?? new ShipPlacmentService();
            _enemyAI = new BakaAI(PlayerField);
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

        public void MakeAIMove()
        {
            if (CurrentPhase != GamePhase.EnemyTurn || PlayerField.AllShipsSunk) return;

            var (x, y) = _enemyAI.MakeMove();
            if (x >= 0 && y >= 0)
            {
                var result = PlayerField.Shoot(x, y);

                LastAIMoveWasHit = (result == CellState.Hit || result == CellState.Sunk);

                if (!LastAIMoveWasHit)
                {
                    CurrentPhase = GamePhase.PlayerTurn;
                }
            }
        }

        public void StartGame()
        {
            if (CurrentPhase != GamePhase.Setup) return;
            CurrentPhase = GamePhase.PlayerTurn;
            GameStateChanged?.Invoke(CurrentPhase, "Игра началась!");
        }
        
        public void ResetGame()
        {
            PlayerField = new GameField();
            EnemyField = new GameField();
            _enemyAI = new BakaAI(PlayerField);
            CurrentPhase = GamePhase.Setup;
        }
    }
}