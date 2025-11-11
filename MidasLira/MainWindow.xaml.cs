using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MidasLira
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Обработчик выбора передачи жесткостей плитных фундаментов
        private void PlateFoundationButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно для плитных фундаментов в модальном режиме
            var plateWindow = new PlateFoundationWindow();
            plateWindow.Owner = this; // Устанавливаем владельца окна
            plateWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Центровка окна
            plateWindow.ShowDialog(); // Модальное открытие окна
        }

        // Обработчик выбора передачи жесткостей свайных фундаментов
        private void PileFoundationButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно-заглушку для свайных фундаментов в модальном режиме
            var pileWindow = new PileFoundationStubWindow();
            pileWindow.Owner = this; // Устанавливаем владельца окна
            pileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Центровка окна
            pileWindow.ShowDialog(); // Модальное открытие окна
        }
    }
}