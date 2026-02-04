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
        private readonly RigidityCalculator _rigidityCalculator;
        private readonly ExcelReader _excelReader;
        private readonly Logger _logger;

        public DataProcessor(RigidityCalculator rigidityCalculator, Writer writer,
                           ExcelReader excelReader, Logger logger)
        {
            _rigidityCalculator = rigidityCalculator ?? throw new ArgumentNullException(nameof(rigidityCalculator));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _excelReader = excelReader ?? throw new ArgumentNullException(nameof(excelReader));
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

                List<MidasNodeInfo> midasNodes = new List<MidasNodeInfo>();
                List<LiraNodeInfo> liraNodes = new List<LiraNodeInfo>();
                List<MidasElementInfo> midasElements = new List<MidasElementInfo>();
                List<LiraElementInfo> liraElements = new List<LiraElementInfo>();
                List<Plaque> plaques = new List<Plaque>();

                try
                {
                    _logger.Info($"Обработка файлов:\n  Excel: {excelFilePath}\n  ЛИРА: {liraSaprFilePath}");

                    // Шаг 1: Чтение данных из Excel
                    _logger.StartOperation("Чтение данных из Excel");
                    (midasNodes, liraNodes, midasElements, liraElements) = _excelReader.ReadFromExcel(excelFilePath);
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
                    plaques = _rigidityCalculator.CalculateNodeRigidities(midasNodes, midasElements);
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
            _logger.Debug($"Валидация входных параметров");

            if (string.IsNullOrWhiteSpace(excelFilePath))
            {
                throw new ArgumentException("Путь к файлу Excel не может быть пустым.", nameof(excelFilePath));
            }

            if (string.IsNullOrWhiteSpace(liraSaprFilePath))
            {
                throw new ArgumentException("Путь к файлу ЛИРА-САПР не может быть пустым.", nameof(liraSaprFilePath));
            }

            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException($"Файл Excel не найден: {excelFilePath}");
            }

            if (!File.Exists(liraSaprFilePath))
            {
                throw new FileNotFoundException($"Файл ЛИРА-САПР не найден: {liraSaprFilePath}");
            }

            _logger.Debug($"Параметры валидны");
        }
    }
}