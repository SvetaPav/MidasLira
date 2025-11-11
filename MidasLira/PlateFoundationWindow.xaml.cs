using Microsoft.Win32;
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
using System.IO;


namespace MidasLira
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class PlateFoundationWindow : Window
    {

        private readonly DataProcessor _dataProcessor;
        private readonly Logger _logger;

        public PlateFoundationWindow()
        {
            InitializeComponent();
            _dataProcessor = new DataProcessor(new RigidityCalculator(), new Writer(new PositionFinder()), new ExcelReader());
            _logger = new Logger();
        }

        // Обработчик кнопки "Выбрать файл Excel"
        private void SelectExcelButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Excel Files (*.xls)|*.xls";
            if (dialog.ShowDialog() == true)
            {
                ExcelFileTextBox.Text = dialog.FileName;
            }
        }

        // Обработчик кнопки "Выбрать файл ЛИРА-САПР"
        private void SelectLirasaprButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Text Files (*.txt)|*.txt";
            if (dialog.ShowDialog() == true)
            {
                LirasaprFileTextBox.Text = dialog.FileName;
            }
        }

        // Обработчик кнопки "Обработать"
        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var inputFile = ExcelFileTextBox.Text;
                var outputFile = LirasaprFileTextBox.Text;

                if (_dataProcessor.ProcessFile(inputFile, outputFile))
                {
                    MessageBox.Show("Данные успешно обработаны!");
                }
                else
                {
                    MessageBox.Show("Ошибка при обработке данных.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Error", $"Ошибка обработки: {ex.Message}");
                MessageBox.Show($"Возникла ошибка: {ex.Message}");
            }
        }

        // Обработчик события закрытия окна
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); 
        }
    }
}
