using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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

namespace MidasLira
{
    /// <summary>
    /// Модель прогресса для привязки
    /// </summary>
    public class ProgressModel : INotifyPropertyChanged
    {
        private double _progress;
        private string? _status;
        private bool _isIndeterminate;

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
            }
        }

        public string? Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                _isIndeterminate = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class PlateFoundationWindow : Window
    {
        private readonly DataProcessor _dataProcessor;
        private readonly Logger _logger;
        private ProgressModel _progressModel;
        private bool _isProcessing;

        public PlateFoundationWindow()
        {
            InitializeComponent();
            _logger = new Logger(enableConsoleOutput: true); // Включаем вывод в консоль для отладки
            _progressModel = new ProgressModel();

            // Устанавливаем контекст данных для привязки
            this.DataContext = _progressModel;

            _dataProcessor = new DataProcessor(
                new Writer(new PositionFinder(), _logger),
                _logger);

            // Привязка ProgressBar к модели
            ProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding("Progress") { Mode = BindingMode.OneWay });
            ProgressText.SetBinding(TextBlock.TextProperty, new Binding("Status") { Mode = BindingMode.OneWay });

            UpdateUIState();

        }

        private void SelectExcelButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.Debug("Кнопка выбора файла Excel нажата");

            OpenFileDialog dialog = new()
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|Excel Files (*.xls)|*.xls|All files (*.*)|*.*",
                Title = "Выберите файл Excel с данными MIDAS"
            };


            if (dialog.ShowDialog() == true)
            {
                ExcelFileTextBox.Text = dialog.FileName;
                _logger.Info($"Выбран файл Excel: {dialog.FileName}");
            }
            else
            {
                _logger.Debug("Выбор файла Excel отменен пользователем");
            }
        }

        private void SelectLirasaprButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.Debug("Кнопка выбора файла ЛИРА-САПР нажата");

            OpenFileDialog dialog = new()
            {
                Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Выберите файл ЛИРА-САПР"
            };

            if (dialog.ShowDialog() == true)
            {
                LirasaprFileTextBox.Text = dialog.FileName;
                _logger.Info($"Выбран файл ЛИРА-САПР: {dialog.FileName}");
            }
            else
            {
                _logger.Debug("Выбор файла ЛИРА-САПР отменен пользователем");
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return; // Защита от двойного нажатия
            _logger.Info("Нажата кнопка 'Обработать'");

            // Сохраняем исходное состояние курсора
            var originalCursor = Cursor;

            try
            {
                var inputFile = ExcelFileTextBox.Text;
                var outputFile = LirasaprFileTextBox.Text;

                // ВАЛИДАЦИЯ ВВОДА
                if (string.IsNullOrEmpty(inputFile))
                {
                    _logger.Warning("Не выбран файл Excel");
                    MessageBox.Show("Пожалуйста, выберите файл Excel.", "Внимание",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(outputFile))
                {
                    _logger.Warning("Не выбран файл ЛИРА-САПР");
                    MessageBox.Show("Пожалуйста, выберите файл ЛИРА-САПР.", "Внимание",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем существование файлов
                if (!File.Exists(inputFile))
                {
                    _logger.Warning($"Файл Excel не найден: {inputFile}");
                    MessageBox.Show($"Файл Excel не найден:\n{inputFile}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(outputFile))
                {
                    _logger.Warning($"Файл ЛИРА-САПР не найден: {outputFile}");
                    MessageBox.Show($"Файл ЛИРА-САПР не найден:\n{outputFile}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _logger.Info("Начало обработки данных");

                // ОТКЛЮЧАЕМ КНОПКУ И МЕНЯЕМ КУРСОР НА ВРЕМЯ ОБРАБОТКИ 
                if(sender is Button button)
                {
                    button.IsEnabled = false;
                }
                else
                {
                    // Если sender не кнопка, ищем кнопку по имени
                    ProcessButton.IsEnabled = false;
                }

                // НАСТРАИВАЕМ UI ДЛЯ ОБРАБОТКИ
                _isProcessing = true;
                UpdateUIState();

                // Сбрасываем прогресс
                ResetProgress();

                Cursor = Cursors.Wait;

                // ЗАПУСК АСИНХРОННОЙ ОБРАБОТКИ
                bool success = await Task.Run(() =>
                {
                    try
                    {
                        // Создаем объект для отслеживания прогресса
                        var progress = new Progress<(double Progress, string Status)>(update =>
                        {
                            // Этот код выполняется в UI-потоке
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _progressModel.Progress = update.Progress;
                                _progressModel.Status = update.Status;
                            });
                        });

                        // Вызываем метод с прогрессом
                        return _dataProcessor.ProcessFile(inputFile, outputFile, progress);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Ошибка в фоновом потоке: {ex.Message}", ex);
                        return false;
                    }
                });

                if (success)
                {
                    _logger.Info("Обработка завершена успешно");
                    ShowStatus("Обработка завершена успешно!", Colors.Green);

                    MessageBox.Show(
                        $"Данные успешно обработаны и записаны в файл!\n\n" +
                        $"Логи сохранены в папке Logs рядом с программой.\n" +
                        $"Вы можете открыть логи через меню 'Файл → Открыть логи'.",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    _logger.Error("Обработка завершилась с ошибкой");
                    ShowStatus("Ошибка при обработке данных", Colors.Red);
                    MessageBox.Show("Ошибка при обработке данных. Проверьте файл логов для подробностей.",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Критическая ошибка при обработке", ex);
                ShowStatus($"Ошибка: {ex.Message}", Colors.Red);
                MessageBox.Show($"Возникла ошибка:\n{ex.Message}\n\nПроверьте файл логов для подробностей.",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // ВОССТАНАВЛИВАЕМ ИНТЕРФЕЙС
                _isProcessing = false;
                UpdateUIState();

                if (sender is Button button)
                {
                    button.IsEnabled = true;
                }
                else
                {
                    ProcessButton.IsEnabled = true;
                }
                    
                Cursor = originalCursor;
                _logger.Info("Интерфейс восстановлен");

                // Через 5 секунд скрываем статус
                _ = HideStatusAfterDelayAsync(5000);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("Закрытие окна плитных фундаментов");
            this.Close();
        }

        #region Методы для работы с прогрессом

        /// <summary>
        /// Сброс прогресса
        /// </summary>
        private void ResetProgress()
        {
            _progressModel.Progress = 0;
            _progressModel.Status = "Начало обработки...";
            _progressModel.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = "";
        }

        /// <summary>
        /// Показать финальный статус
        /// </summary>
        private void ShowStatus(string message, Color color)
        {
            _progressModel.Progress = 100;
            _progressModel.Status = "Готово";
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush(color);
            ProgressBar.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Скрыть статус через задержку
        /// </summary>
        private async Task HideStatusAfterDelayAsync(int milliseconds)
        {
            await Task.Delay(milliseconds);
            if (!_isProcessing)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "";
                });
            }
        }

        /// <summary>
        /// Обновить состояние UI элементов
        /// </summary>
        private void UpdateUIState()
        {
            bool isEnabled = !_isProcessing;

            ProcessButton.IsEnabled = isEnabled;
            ProcessButton.Opacity = isEnabled ? 1.0 : 0.6;
            ProcessButton.Content = _isProcessing ? "Обработка..." : "Обработать";

            ExcelFileTextBox.IsEnabled = isEnabled;
            LirasaprFileTextBox.IsEnabled = isEnabled;
        }

        #endregion
    }
}