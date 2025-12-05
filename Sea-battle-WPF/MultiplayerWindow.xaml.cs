using Sea_battle_WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Sea_battle_WPF
{
    public partial class MultiplayerWindow : Window
    {
        public MultiplayerWindow()
        {
            InitializeComponent();
            DataContext = new MultiplayerViewModel();
        }
    }
}