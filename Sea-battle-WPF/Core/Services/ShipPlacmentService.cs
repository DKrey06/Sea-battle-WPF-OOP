using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sea_battle_WPF.Core.Services
{
    public class ShipPlacmentService
    {
        private readonly Random _random = new Random();

        public void PlaceShipAutomatically(GameField field)
        {

        }
        public void PlaceSipRandomly(GameField field, int size) 
        {
            bool placed = false;
            int attempts = 0;

            while (!placed && attempts < 100) 
            {
                int x = _random.Next(0, field.Size);
                int y = _random.Next(0, field.Size);
                var direction = (ShipDirection)_random.Next(0, 2);

                if(field.CanPlaceShip(x, y, size, direction))
                {
                    field.PlaceShip(x, y, size, direction);
                    placed = true;
                }
                attempts++;
            }
        }
        public bool ValidateManualPlacement(GameField field, List<Ship> ships)
        {
            var requiredShips = new Dictionary<int, int> { { 4, 1 }, { 3, 2 }, { 2, 3 }, { 1, 4 } };
            var actualShips = ships.GroupBy(s => s.Size).ToDictionary(g => g.Key, g => g.Count());

            foreach (var required in requiredShips)
            {
                if (!actualShips.ContainsKey(required.Key)|| actualShips[required.Key] != required.Value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
