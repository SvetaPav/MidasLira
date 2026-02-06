using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MidasLira.Mapper;

namespace MidasLira
{
    public class DataProcessor
    {
        private readonly Writer _writer;
        private readonly Logger _logger;

        public DataProcessor(Writer writer, Logger logger)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Главный метод обработки данных.
        /// </summary>
        public bool ProcessFile(string excelFilePath, string liraSaprFilePath)
        {
            return _logger.LogExecutionTime("Обработка файлов", () =>
            {
                // ВАЛИДАЦИЯ ВХОДНЫХ ПАРАМЕТРОВ
                ValidateInputParameters(excelFilePath, liraSaprFilePath);

                List<MidasNodeInfo> midasNodes = [];
                List<LiraNodeInfo> liraNodes = [];
                List<MidasElementInfo> midasElements = [];
                List<LiraElementInfo> liraElements = [];
                List<Plaque> plaques = [];

                try
                {
                    _logger.Info($"Обработка файлов:\n  Excel: {excelFilePath}\n  ЛИРА: {liraSaprFilePath}");

                    // Шаг 1: Чтение данных из Excel
                    _logger.StartOperation("Чтение данных из Excel");
                    (midasNodes, liraNodes, midasElements, liraElements) = ExcelReader.ReadFromExcel(excelFilePath);
                    _logger.Info($"Прочитано: {midasNodes.Count} узлов MIDAS, {liraNodes.Count} узлов ЛИРА, " +
                               $"{midasElements.Count} элементов MIDAS, {liraElements.Count} элементов ЛИРА");
                    _logger.EndOperation("Чтение данных из Excel");

                    // Шаг 2: Сопоставление узлов и элементов
                    _logger.StartOperation("Сопоставление узлов и элементов");
                    MapNodesAndElements(midasNodes, liraNodes, midasElements, liraElements);

                    // АНАЛИЗ СОПОСТАВЛЕНИЯ
                    int matchedNodesCount = midasNodes.Count(n => n.AppropriateLiraNode.Id != 0);
                    int matchedElementsCount = midasElements.Count(e => e.AppropriateLiraElement.Id != 0);

                    _logger.Info($"Сопоставлено: {matchedNodesCount}/{midasNodes.Count} узлов " +
                               $"({(double)matchedNodesCount / midasNodes.Count * 100:F1}%)");
                    _logger.Info($"Сопоставлено: {matchedElementsCount}/{midasElements.Count} элементов " +
                               $"({(double)matchedElementsCount / midasElements.Count * 100:F1}%)");

                    if (matchedNodesCount == 0)
                    {
                        throw new InvalidOperationException("Не удалось сопоставить ни один узел. " +
                                                          "Проверьте координаты в файлах Excel.");
                    }

                    _logger.EndOperation("Сопоставление узлов и элементов");

                    // Шаг 3: Расчет жесткостей
                    _logger.StartOperation("Расчет жесткостей узлов");
                    plaques = RigidityCalculator.CalculateNodeRigidities(midasNodes, midasElements);
                    _logger.Info($"Рассчитано жесткостей для {plaques.Count} плит");
                    _logger.EndOperation("Расчет жесткостей узлов");

                    // Шаг 4: Запись данных в файл ЛИРА-САПР
                    _logger.StartOperation("Запись данных в файл ЛИРА-САПР");
                    _writer.WriteNodeAndBeddingData(liraSaprFilePath, midasNodes, midasElements, plaques);
                    _logger.Info($"Данные успешно записаны в файл");
                    _logger.EndOperation("Запись данных в файл ЛИРА-САПР");

                    _logger.Info($"ОБРАБОТКА ЗАВЕРШЕНА УСПЕШНО");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Ошибка при обработке файлов", ex);

                    // Логируем дополнительную информацию для отладки
                    if (midasNodes != null)
                    {
                        _logger.Debug($"midasNodes.Count = {midasNodes.Count}");
                    }
                    if (midasElements != null)
                    {
                        _logger.Debug($"midasElements.Count = {midasElements.Count}");
                    }

                    throw new InvalidOperationException(
                        $"Ошибка при обработке файлов. Excel: {excelFilePath}, ЛИРА: {liraSaprFilePath}", ex);
                }
            });
        }

        private void ValidateInputParameters(string excelFilePath, string liraSaprFilePath)
        {
            _logger.Debug("Начало валидации входных параметров");

            try
            {
                // Проверка на null/пустоту
                ArgumentException.ThrowIfNullOrWhiteSpace(excelFilePath, nameof(excelFilePath));
                ArgumentException.ThrowIfNullOrWhiteSpace(liraSaprFilePath, nameof(liraSaprFilePath));

                // Проверка расширения файлов
                ValidateFileExtension(excelFilePath, [".xlsx", ".xls"], "Excel");
                ValidateFileExtension(liraSaprFilePath, [".txt"], "ЛИРА-САПР");

                // Проверка существования файлов
                ValidateFileExists(excelFilePath, "Excel");
                ValidateFileExists(liraSaprFilePath, "ЛИРА-САПР");

                _logger.Debug("Параметры успешно прошли валидацию");
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка валидации параметров: {ex.Message}", ex);
                throw;
            }
        }

        private static void ValidateFileExtension(string filePath, string[] validExtensions, string fileType)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (!validExtensions.Contains(extension))
            {
                throw new ArgumentException(
                    $"Файл {fileType} должен иметь одно из расширений: {string.Join(", ", validExtensions)}. " +
                    $"Получено: {extension}",
                    Path.GetFileName(filePath));
            }
        }

        private static void ValidateFileExists(string filePath, string fileType)
        {
            if (!File.Exists(filePath))
            {
                string absolutePath = Path.GetFullPath(filePath);
                string errorMessage = $"Файл {fileType} не найден:\n" +
                                     $"Исходный путь: {filePath}\n" +
                                     $"Абсолютный путь: {absolutePath}\n" +
                                     $"Рабочая директория: {Environment.CurrentDirectory}";

                throw new FileNotFoundException(errorMessage, filePath);
            }

            // Дополнительно: проверка прав доступа
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException($"Файл {fileType} заблокирован или недоступен для чтения/записи: {filePath}", ex);
            }
        }
    }
}