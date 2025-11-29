using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sea_battle_WPF.Core.Enums;
using Sea_battle_WPF.Core.Models;

namespace Sea_battle_WPF.Core.GameAI
{
    public abstract class AIBase
    {
        protected readonly Random _random;
        protected GameField _playerField;

        public AIBase(GameField playerField) 
        {
            _random = new Random();
            _playerField = playerField;

        }
        public abstract (int x, int y) MakeMove();

        public virtual void Reset()
        {

        }

    }
}
