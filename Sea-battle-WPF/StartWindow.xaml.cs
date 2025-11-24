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
    public partial class StartWindow : Window
    {
        public StartWindow()
        {
            InitializeComponent();
        }

        private void StartSinglePlayer_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow(false); //false - игра с компьютером
            mainWindow.Show();
            this.Close();
        }

        private void StartMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow(true); //true - сетевая игра
            mainWindow.Show();
            this.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}