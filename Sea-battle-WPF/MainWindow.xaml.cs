using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sea_battle_WPF
{
    public partial class MainWindow : Window
    {
         //private bool isMultiplayer;

        public class CellData
        {
            public Brush CellColor { get; set; }
            public bool IsClickable { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeGameFields();
        }

        private void InitializeGameFields()
        {
            for (int i = 0; i < 100; i++)
            {
                PlayerField.Items.Add(new CellData { CellColor = Brushes.Transparent, IsClickable = false });
                EnemyField.Items.Add(new CellData { CellColor = Brushes.Transparent, IsClickable = true });
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Visible;
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
        }

        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            GameStatusText.Text = "Ваш ход";
            //Здесь будет логика начала игры
        }

        private void AutoArrange_Click(object sender, RoutedEventArgs e)
        {
            GameStatusText.Text = "Корабли расставлены";
            //Здесь будет логика автоматической расстановки кораблей
        }

        private void Surrender_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Вы уверены, что хотите сдаться?", "Сдаться",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                GameStatusText.Text = "Вы сдались";
                //Здесь будет логика завершения игры
            }
        }
    }
}