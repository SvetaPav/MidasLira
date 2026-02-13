using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MidasLira.Mapper;

namespace MidasLira
{
    /// <summary>
    /// Класс, управляющий полным циклом обработки:
    /// чтение Excel → сопоставление → расчёт жесткостей → запись в ЛИРА‑САПР.
    /// </summary>
    public class DataProcessor(Writer writer, Logger logger)
    {
        private readonly Writer _writer = writer ?? throw new ArgumentNullException(nameof(writer));  // Используем основной конструктор
        private readonly Logger _logger = logger ?? throw new ArgumentNullException(nameof(logger));


        // ---------------------------------------------------------------------
        //  ПУБЛИЧНЫЙ МЕТОД – ТОЧКА ВХОДА
        // ---------------------------------------------------------------------

        /// <summary>
        /// Выполняет полный цикл обработки данных с отчётом о прогрессе.
        /// </summary>
        public bool ProcessFile(
    string excelFilePath,
    string liraSaprFilePath,
    IProgress<(double Progress, string Status)>? progress = null)
        {
            return _logger.LogExecutionTime("ОБРАБОТКА ФАЙЛОВ", () =>
            {
                try
                {
                    // ---------------------------------------------------------
                    // 1. Валидация входных параметров
                    // ---------------------------------------------------------
                    ValidateInputParameters(excelFilePath, liraSaprFilePath);
                    ReportProgress(progress, 0, "Валидация пройдена");

                    // ---------------------------------------------------------
                    // 2. Чтение данных из Excel
                    // ---------------------------------------------------------
                    var (midasNodes, liraNodes, midasElements, liraElements) =
                        ReadExcelData(excelFilePath, progress);

                    // ---------------------------------------------------------
                    // 3. Сопоставление узлов и элементов
                    // ---------------------------------------------------------
                    (int matchedNodes, int matchedElements) =
                        PerformMapping(midasNodes, liraNodes, midasElements, liraElements, progress);

                    // ---------------------------------------------------------
                    // 4. Расчёт жесткостей узлов (плиты)
                    // ---------------------------------------------------------
                    List<Plaque> plaques = CalculateRigidities(midasNodes, midasElements, progress);

                    // ---------------------------------------------------------
                    // 5. Запись результатов в файл ЛИРА-САПР
                    // ---------------------------------------------------------
                    WriteResults(liraSaprFilePath, midasNodes, midasElements, plaques, progress);

                    // ---------------------------------------------------------
                    // 6. Успешное завершение
                    // ---------------------------------------------------------
                    _logger.Info("ОБРАБОТКА ЗАВЕРШЕНА УСПЕШНО");
                    ReportProgress(progress, 100, "Готово");
                    return true;
                }
                catch (DataProcessorException)
                {
                    // Исключения этого типа уже содержат всю нужную информацию,
                    // логирование выполнено на месте, просто пробрасываем дальше.
                    throw;
                }
                catch (Exception ex)
                {
                    // Непредвиденная ошибка – оборачиваем в общее исключение обработки
                    _logger.Error("Критическая необработанная ошибка", ex);
                    ReportProgress(progress, 0, $"Внутренняя ошибка: {ex.Message}");
                    throw new DataProcessorException(
                        stage: "Общий",
                        message: "Произошла непредвиденная ошибка. Подробности в логе.",
                        innerException: ex);
                }
            });
        }

        // ---------------------------------------------------------------------
        //  ПРИВАТНЫЕ МЕТОДЫ – ДЕТАЛИ ЭТАПОВ
        // ---------------------------------------------------------------------

        #region Этап 1. Валидация

        private void ValidateInputParameters(string excelFilePath, string liraSaprFilePath)
        {
            _logger.Debug("Начало валидации входных параметров");

            try
            {
                // Проверка на null или пустую строку
                ArgumentException.ThrowIfNullOrWhiteSpace(excelFilePath, nameof(excelFilePath));
                ArgumentException.ThrowIfNullOrWhiteSpace(liraSaprFilePath, nameof(liraSaprFilePath));

                // Проверка расширений
                ValidateFileExtension(excelFilePath, [".xlsx", ".xls"], "Excel");
                ValidateFileExtension(liraSaprFilePath, [".txt"], "ЛИРА-САПР");

                // Проверка существования файлов и доступности
                ValidateFileExists(excelFilePath, "Excel");
                ValidateFileExists(liraSaprFilePath, "ЛИРА-САПР");

                _logger.Debug("Валидация параметров успешно завершена");
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка валидации: {ex.Message}", ex);
                throw new ValidationException("Некорректные входные параметры", ex);
            }
        }

        private static void ValidateFileExtension(string filePath, string[] validExtensions, string fileType)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!validExtensions.Contains(extension))
            {
                throw new ArgumentException(
                    $"Файл {fileType} должен иметь расширение: {string.Join(", ", validExtensions)}. " +
                    $"Текущее: {extension}",
                    Path.GetFileName(filePath));
            }
        }

        private static void ValidateFileExists(string filePath, string fileType)
        {
            if (!File.Exists(filePath))
            {
                string absolute = Path.GetFullPath(filePath);
                throw new FileNotFoundException(
                    $"Файл {fileType} не найден.\n" +
                    $"Исходный путь: {filePath}\n" +
                    $"Абсолютный путь: {absolute}\n" +
                    $"Рабочая директория: {Environment.CurrentDirectory}",
                    filePath);
            }

            // Проверка, что файл не заблокирован другим процессом
            try
            {
                using var _ = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException($"Файл {fileType} заблокирован или недоступен: {filePath}", ex);
            }
        }

        #endregion

        #region Этап 2. Чтение Excel

        private (
            List<MidasNodeInfo> midasNodes,
            List<LiraNodeInfo> liraNodes,
            List<MidasElementInfo> midasElements,
            List<LiraElementInfo> liraElements)
            ReadExcelData(string excelFilePath, IProgress<(double, string)>? progress)
        {
            try
            {
                ReportProgress(progress, 10, "Чтение данных из Excel...");
                _logger.StartOperation("Чтение Excel");

                var reader = new ExcelReader(_logger);
                var (midasNodes, liraNodes, midasElements, liraElements) =
                    reader.ReadFromExcel(excelFilePath);

                _logger.Info($"Прочитано: узлов MIDAS = {midasNodes.Count}, ЛИРА = {liraNodes.Count}, " +
                             $"элементов MIDAS = {midasElements.Count}, ЛИРА = {liraElements.Count}");

                ReportProgress(progress, 20,
                    $"Прочитано {midasNodes.Count} узлов и {midasElements.Count} элементов");

                _logger.EndOperation("Чтение Excel");
                return (midasNodes, liraNodes, midasElements, liraElements);
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка при чтении Excel", ex);
                throw new ExcelReadException("Не удалось прочитать данные из Excel-файла", ex);
            }
        }

        #endregion

        #region Этап 3. Сопоставление

        private (int matchedNodesCount, int matchedElementsCount)
            PerformMapping(
                List<MidasNodeInfo> midasNodes,
                List<LiraNodeInfo> liraNodes,
                List<MidasElementInfo> midasElements,
                List<LiraElementInfo> liraElements,
                IProgress<(double, string)>? progress)
        {
            try
            {
                ReportProgress(progress, 30, "Сопоставление узлов и элементов...");
                _logger.StartOperation("Сопоставление");

                // Вызов статического метода маппера
                MapNodesAndElements(midasNodes, liraNodes, midasElements, liraElements);

                int matchedNodes = midasNodes.Count(n => !n.AppropriateLiraNode.IsEmpty);
                int matchedElements = midasElements.Count(e => !e.AppropriateLiraElement.IsEmpty);

                double nodePercent = midasNodes.Count == 0 ? 0 : (double)matchedNodes / midasNodes.Count * 100;
                double elemPercent = midasElements.Count == 0 ? 0 : (double)matchedElements / midasElements.Count * 100;

                _logger.Info($"Сопоставлено узлов: {matchedNodes}/{midasNodes.Count} ({nodePercent:F1}%)");
                _logger.Info($"Сопоставлено элементов: {matchedElements}/{midasElements.Count} ({elemPercent:F1}%)");

                ReportProgress(progress, 40,
                    $"Сопоставлено {matchedNodes} узлов и {matchedElements} элементов");

                // Критические проверки – отсутствие сопоставлений делает дальнейшую работу бессмысленной
                if (matchedNodes == 0)
                    throw new MappingException("Не сопоставлен ни один узел. Проверьте координаты в Excel.");

                if (matchedElements == 0)
                    throw new MappingException("Не сопоставлен ни один элемент. Проверьте связность узлов элементов.");

                if (matchedElements < midasElements.Count * 0.1)
                    _logger.Warning($"Сопоставлено менее 10% элементов ({matchedElements} из {midasElements.Count})");

                _logger.EndOperation("Сопоставление");
                return (matchedNodes, matchedElements);
            }
            catch (MappingException)
            {
                throw; // уже содержит нужный контекст
            }
            catch (Exception ex)
            {
                _logger.Error("Критическая ошибка при сопоставлении", ex);
                throw new MappingException("Ошибка во время сопоставления данных", ex);
            }
        }

        #endregion

        #region Этап 4. Расчёт жесткостей

        private List<Plaque> CalculateRigidities(
            List<MidasNodeInfo> midasNodes,
            List<MidasElementInfo> midasElements,
            IProgress<(double, string)>? progress)
        {
            try
            {
                ReportProgress(progress, 50, "Расчёт жесткостей узлов...");
                _logger.StartOperation("Расчёт жесткостей");

                var plaques = RigidityCalculator.CalculateNodeRigidities(midasNodes, midasElements);

                _logger.Info($"Рассчитано жесткостей для {plaques.Count} плит");
                ReportProgress(progress, 70, $"Рассчитано жесткостей для {plaques.Count} плит");

                _logger.EndOperation("Расчёт жесткостей");
                return plaques;
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка при расчёте жесткостей", ex);
                throw new CalculationException("Не удалось вычислить жесткости узлов", ex);
            }
        }

        #endregion

        #region Этап 5. Запись результатов

        private void WriteResults(
            string liraSaprFilePath,
            List<MidasNodeInfo> midasNodes,
            List<MidasElementInfo> midasElements,
            List<Plaque> plaques,
            IProgress<(double, string)>? progress)
        {
            try
            {
                ReportProgress(progress, 80, "Запись данных в файл ЛИРА-САПР...");
                _logger.StartOperation("Запись в ЛИРА");

                _writer.WriteNodeAndBeddingData(liraSaprFilePath, midasNodes, midasElements, plaques);

                _logger.Info("Данные успешно записаны в файл ЛИРА-САПР");
                ReportProgress(progress, 100, "Данные успешно записаны");

                _logger.EndOperation("Запись в ЛИРА");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка при записи в файл ЛИРА-САПР", ex);
                throw new WriteException("Не удалось записать данные в файл ЛИРА-САПР", ex);
            }
        }

        #endregion

        #region Вспомогательные методы

        private static void ReportProgress(IProgress<(double, string)>? progress, double value, string status)
        {
            progress?.Report((value, status));
        }

        #endregion
    }


    // -------------------------------------------------------------------------
    //  ИСКЛЮЧЕНИЯ ДЛЯ ДЕТАЛЬНОЙ ДИАГНОСТИКИ
    //  (можно вынести в отдельный файл, но для компактности размещены здесь)
    // -------------------------------------------------------------------------

    /// <summary> Базовый класс для всех исключений, возникающих при обработке данных </summary>
    public class DataProcessorException(string stage, string message, Exception? innerException = null) : Exception(message, innerException)
    {
        public string Stage { get; } = stage;
    }

    /// <summary> Ошибка на этапе валидации входных параметров </summary>
    public class ValidationException(string message, Exception? innerException = null) : DataProcessorException("Валидация", message, innerException)
    {
    }

    /// <summary> Ошибка при чтении Excel-файла </summary>
    public class ExcelReadException(string message, Exception? innerException = null) : DataProcessorException("Чтение Excel", message, innerException)
    {
    }

    /// <summary> Ошибка при сопоставлении узлов/элементов </summary>
    public class MappingException(string message, Exception? innerException = null) : DataProcessorException("Сопоставление", message, innerException)
    {
    }

    /// <summary> Ошибка при расчёте жесткостей </summary>
    public class CalculationException(string message, Exception? innerException = null) : DataProcessorException("Расчёт жесткостей", message, innerException)
    {
    }

    /// <summary> Ошибка при записи в файл ЛИРА-САПР </summary>
    public class WriteException(string message, Exception? innerException = null) : DataProcessorException("Запись в ЛИРА", message, innerException)
    {
    }
}
