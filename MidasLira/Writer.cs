using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static MidasLira.Mapper;

namespace MidasLira
{
    public class Writer
    {
        private readonly PositionFinder _positionFinder;
        private readonly Logger _logger;

        public Writer(PositionFinder positionFinder, Logger logger = null)
        {
            _positionFinder = positionFinder ?? throw new ArgumentNullException(nameof(positionFinder));
            _logger = logger ?? new Logger(); // Создаем логгер по умолчанию если не передан
        }

        /// <summary>
        /// Главный метод записи данных в файл ЛИРА-САПР
        /// </summary>
        public bool WriteNodeAndBeddingData(string filePath, List<MidasNodeInfo> nodes,
                                           List<MidasElementInfo> elements, List<Plaque> plaques)
        {
            return _logger.LogExecutionTime("Запись данных в ЛИРА-САПР", () =>
            {
                try
                {
                    _logger.Info($"Начало записи данных в файл: {filePath}");

                    // ВАЛИДАЦИЯ
                    ValidateInputData(filePath, nodes, elements, plaques);

                    // СОЗДАЕМ РЕЗЕРВНУЮ КОПИЮ
                    CreateBackup(filePath);

                    // АНАЛИЗИРУЕМ СТРУКТУРУ ФАЙЛА
                    _logger.StartOperation("Анализ структуры файла ЛИРА-САПР");
                    var parseResult = _positionFinder.ParseTextFile(filePath);
                    _logger.Info($"Структура файла проанализирована успешно");
                    _logger.EndOperation("Анализ структуры файла ЛИРА-САПР");

                    // ЧИТАЕМ ВЕСЬ ФАЙЛ
                    var lines = File.ReadAllLines(filePath).ToList();

                    // 1. ЗАПИСЫВАЕМ ЖЕСТКОСТИ УЗЛОВ В РАЗДЕЛ (3/)
                    WriteStiffnessesToFile(ref lines, nodes, plaques, parseResult);

                    // 2. ЗАПИСЫВАЕМ ЭЛЕМЕНТЫ КЭ56 В РАЗДЕЛ (1/)
                    WriteElement56ToFile(ref lines, nodes, parseResult);

                    // 3. ЗАПИСЫВАЕМ КОЭФФИЦИЕНТЫ ПОСТЕЛИ В РАЗДЕЛ (19/)
                    WriteBeddingCoefficientsToFile(ref lines, elements, parseResult);

                    // ЗАПИСЫВАЕМ ОБНОВЛЕННЫЙ ФАЙЛ
                    _logger.StartOperation("Запись обновленного файла на диск");
                    File.WriteAllLines(filePath, lines, Encoding.UTF8);
                    _logger.Info($"Файл успешно сохранен: {filePath}, строк: {lines.Count}");
                    _logger.EndOperation("Запись обновленного файла на диск");

                    // ГЕНЕРИРУЕМ ОТЧЕТ
                    var report = GenerateReport(nodes, elements);
                    _logger.Info($"Отчет о записи:\n{report}");

                    _logger.Info($"Все данные успешно записаны в {filePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Ошибка записи в файл ЛИРА-САПР", ex);
                    throw new InvalidOperationException($"Ошибка записи в файл ЛИРА-САПР: {ex.Message}", ex);
                }
            });
        }

        // ==================== ЗАПИСЬ ЖЕСТКОСТЕЙ В РАЗДЕЛ (3/) ====================

        private void WriteStiffnessesToFile(ref List<string> lines, List<MidasNodeInfo> nodes,
                                           List<Plaque> plaques, PositionFinder.ParseResult parseResult)
        {
            _logger.StartOperation("Запись жесткостей узлов в раздел (3/)");

            // ФОРМИРУЕМ СТРОКИ С ЖЕСТКОСТЯМИ
            var stiffnessLines = CreateStiffnessLines(nodes, plaques);

            if (!stiffnessLines.Any())
            {
                _logger.Warning("Нет данных о жесткостях для записи");
                _logger.EndOperation("Запись жесткостей узлов в раздел (3/)");
                return;
            }

            // ЕСЛИ РАЗДЕЛА (3/) НЕТ - СОЗДАЕМ ЕГО ПЕРЕД РАЗДЕЛОМ (17/)
            if (parseResult.Section3Start == -1)
            {
                _logger.Info("Раздел (3/) не найден, создаем новый перед разделом (17/)");
                CreateNewSection3(ref lines, stiffnessLines, parseResult);
            }
            else
            {
                // ВСТАВЛЯЕМ ЖЕСТКОСТИ ПОСЛЕ МАТЕРИАЛОВ, ПЕРЕД ЗАКРЫВАЮЩЕЙ СКОБКОЙ
                InsertStiffnessesIntoSection3(ref lines, stiffnessLines, parseResult);
            }

            _logger.Info($"Записано {stiffnessLines.Count} жесткостей узлов");
            _logger.EndOperation("Запись жесткостей узлов в раздел (3/)");
        }

        private List<string> CreateStiffnessLines(List<MidasNodeInfo> nodes, List<Plaque> plaques)
        {
            var stiffnessLines = new List<string>();
            int nextRigidityNumber = 1;

            // СОРТИРУЕМ УЗЛЫ ПО ID ЛИРА-САПР
            var sortedNodes = nodes
                .Where(n => n.AppropriateLiraNode.Id != 0)
                .OrderBy(n => n.AppropriateLiraNode.Id)
                .ToList();

            _logger.Debug($"Найдено {sortedNodes.Count} узлов с сопоставлением для жесткостей");

            foreach (var node in sortedNodes)
            {
                var plaque = plaques.FirstOrDefault(p => p.Nodes.Contains(node));
                double rigidity = plaque?.rigidNodes ?? 0;

                if (rigidity > 0)
                {
                    // ФОРМАТ: "номер_жесткости значение значение 0 0 0 0 /"
                    string line = $"{nextRigidityNumber} {rigidity:F4} {rigidity:F4} 0 0 0 0 /";
                    stiffnessLines.Add(line);

                    node.RigidityNumber = nextRigidityNumber;
                    nextRigidityNumber++;

                    _logger.Debug($"Узел ЛИРА ID={node.AppropriateLiraNode.Id}: жесткость={rigidity:F4}, номер={node.RigidityNumber}");
                }
            }

            return stiffnessLines;
        }

        private void CreateNewSection3(ref List<string> lines, List<string> stiffnessLines,
                                      PositionFinder.ParseResult parseResult)
        {
            // СОЗДАЕМ НОВЫЙ РАЗДЕЛ (3/) ПЕРЕД РАЗДЕЛОМ (17/)
            int insertPosition = parseResult.Section17Start - 1;

            var newSection = new List<string>
            {
                "", // Пустая строка перед разделом
                "( 3/",
                "1 S0 1.05541e+006 20 40/", // Дефолтный материал
                " 0 RO 0.2/",
                " 0 Mu 0.2/"
            };

            newSection.AddRange(stiffnessLines);
            newSection.Add(" )");
            newSection.Add(""); // Пустая строка после раздела

            lines.InsertRange(insertPosition, newSection);

            _logger.Debug($"Создан новый раздел (3/) на позиции {insertPosition}");
        }

        private void InsertStiffnessesIntoSection3(ref List<string> lines, List<string> stiffnessLines,
                                                  PositionFinder.ParseResult parseResult)
        {
            // ВСТАВЛЯЕМ ПОСЛЕ ПОСЛЕДНЕЙ СТРОКИ МАТЕРИАЛОВ, ПЕРЕД ")"
            int insertPosition = parseResult.Section3LastMaterialLine;
            if (insertPosition <= parseResult.Section3Start)
                insertPosition = parseResult.Section3End - 1;

            lines.InsertRange(insertPosition, stiffnessLines);
            _logger.Debug($"Добавлено {stiffnessLines.Count} жесткостей в раздел (3/) на позицию {insertPosition}");
        }

        // ==================== ЗАПИСЬ ЭЛЕМЕНТОВ КЭ56 В РАЗДЕЛ (1/) ====================

        private void WriteElement56ToFile(ref List<string> lines, List<MidasNodeInfo> nodes,
                                         PositionFinder.ParseResult parseResult)
        {
            _logger.StartOperation("Запись элементов КЭ56 в раздел (1/)");

            var element56Lines = CreateElement56Lines(nodes);

            if (!element56Lines.Any())
            {
                _logger.Warning("Нет данных КЭ56 для записи");
                _logger.EndOperation("Запись элементов КЭ56 в раздел (1/)");
                return;
            }

            // РАЗДЕЛ (1/) ДОЛЖЕН БЫТЬ (это обязательный раздел)
            if (parseResult.Section1Start == -1)
            {
                _logger.Error("В файле отсутствует обязательный раздел (1/)");
                throw new InvalidOperationException("В файле отсутствует обязательный раздел (1/)");
            }

            // ВСТАВЛЯЕМ КЭ56 ПОСЛЕ СУЩЕСТВУЮЩИХ ЭЛЕМЕНТОВ, ПЕРЕД ")"
            InsertElement56IntoSection1(ref lines, element56Lines, parseResult);

            _logger.Info($"Записано {element56Lines.Count} элементов КЭ56");
            _logger.EndOperation("Запись элементов КЭ56 в раздел (1/)");
        }

        private List<string> CreateElement56Lines(List<MidasNodeInfo> nodes)
        {
            var element56Lines = new List<string>();

            // ФИЛЬТРУЕМ УЗЛЫ С НОМЕРОМ ЖЕСТКОСТИ
            var validNodes = nodes
                .Where(n => n.AppropriateLiraNode.Id != 0 && n.RigidityNumber > 0)
                .OrderBy(n => n.AppropriateLiraNode.Id)
                .ToList();

            _logger.Debug($"Найдено {validNodes.Count} узлов для создания КЭ56");

            foreach (var node in validNodes)
            {
                // ФОРМАТ: "56 номер_жесткости номер_узла /"
                string line = $"56 {node.RigidityNumber} {node.AppropriateLiraNode.Id} /";
                element56Lines.Add(line);

                _logger.Debug($"КЭ56: узел={node.AppropriateLiraNode.Id}, жесткость={node.RigidityNumber}");
            }

            return element56Lines;
        }

        private void InsertElement56IntoSection1(ref List<string> lines, List<string> element56Lines,
                                                PositionFinder.ParseResult parseResult)
        {
            // ВСТАВЛЯЕМ ПОСЛЕ ПОСЛЕДНЕГО ЭЛЕМЕНТА, ПЕРЕД ")"
            int insertPosition = parseResult.Section1LastElementLine;
            if (insertPosition <= parseResult.Section1Start)
                insertPosition = parseResult.Section1End - 1;

            lines.InsertRange(insertPosition, element56Lines);
            _logger.Debug($"Добавлено {element56Lines.Count} элементов КЭ56 в раздел (1/) на позицию {insertPosition}");
        }

        // ==================== ЗАПИСЬ КОЭФФИЦИЕНТОВ ПОСТЕЛИ В РАЗДЕЛ (19/) ====================

        private void WriteBeddingCoefficientsToFile(ref List<string> lines, List<MidasElementInfo> elements,
                                                   PositionFinder.ParseResult parseResult)
        {
            _logger.StartOperation("Запись коэффициентов постели в раздел (19/)");

            var coefficientLines = CreateCoefficientLines(elements);

            if (!coefficientLines.Any())
            {
                _logger.Warning("Нет коэффициентов постели для записи");
                _logger.EndOperation("Запись коэффициентов постели в раздел (19/)");
                return;
            }

            // ОПРЕДЕЛЯЕМ, КУДА ВСТАВЛЯТЬ
            int insertPosition = _positionFinder.FindPositionForSection19(parseResult);

            if (parseResult.Section19Start == -1)
            {
                // СОЗДАЕМ НОВЫЙ РАЗДЕЛ (19/) ПОСЛЕ РАЗДЕЛА (17/)
                CreateNewSection19(ref lines, coefficientLines, insertPosition);
            }
            else
            {
                // ДОБАВЛЯЕМ К СУЩЕСТВУЮЩЕМУ РАЗДЕЛУ (19/)
                InsertCoefficientsIntoSection19(ref lines, coefficientLines, insertPosition);
            }

            _logger.Info($"Записано {coefficientLines.Count} коэффициентов постели");
            _logger.EndOperation("Запись коэффициентов постели в раздел (19/)");
        }

        private List<string> CreateCoefficientLines(List<MidasElementInfo> elements)
        {
            var coefficientLines = new List<string>();

            // ФИЛЬТРУЕМ ЭЛЕМЕНТЫ С КОЭФФИЦИЕНТАМИ
            var validElements = elements
                .Where(e => e.AppropriateLiraElement.Id != 0 && e.BeddingCoefficient > 0)
                .OrderBy(e => e.AppropriateLiraElement.Id)
                .ToList();

            _logger.Debug($"Найдено {validElements.Count} элементов с коэффициентами постели");

            foreach (var element in validElements)
            {
                // ФОРМАТ: "номер_элемента коэффициент 0 0 0 /"
                string line = $"{element.AppropriateLiraElement.Id} {element.BeddingCoefficient:F3} 0 0 0 /";
                coefficientLines.Add(line);

                _logger.Debug($"Коэффициент постели: элемент={element.AppropriateLiraElement.Id}, C1={element.BeddingCoefficient:F3}");
            }

            return coefficientLines;
        }

        private void CreateNewSection19(ref List<string> lines, List<string> coefficientLines, int insertPosition)
        {
            _logger.Info($"Создаем новый раздел (19/) на позиции {insertPosition}");

            // СОЗДАЕМ НОВЫЙ РАЗДЕЛ (19/) ПОСЛЕ РАЗДЕЛА (17/)
            var newSection = new List<string>
            {
                "", // Пустая строка перед разделом
                "( 19/"
            };

            newSection.AddRange(coefficientLines);
            newSection.Add(" )");
            newSection.Add(""); // Пустая строка после раздела

            // ВСТАВЛЯЕМ НОВЫЙ РАЗДЕЛ
            lines.InsertRange(insertPosition, newSection);
            _logger.Debug($"Создан раздел (19/) с {coefficientLines.Count} коэффициентами");
        }

        private void InsertCoefficientsIntoSection19(ref List<string> lines, List<string> coefficientLines, int insertPosition)
        {
            _logger.Info($"Добавляем коэффициенты в существующий раздел (19/) на позиции {insertPosition}");

            // ВСТАВЛЯЕМ НОВЫЕ КОЭФФИЦИЕНТЫ В СУЩЕСТВУЮЩИЙ РАЗДЕЛ (19/)
            lines.InsertRange(insertPosition, coefficientLines);
            _logger.Debug($"Добавлено {coefficientLines.Count} коэффициентов в раздел (19/)");
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

        private void ValidateInputData(string filePath, List<MidasNodeInfo> nodes,
                                      List<MidasElementInfo> elements, List<Plaque> plaques)
        {
            _logger.StartOperation("Валидация входных данных");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Error("Путь к файлу не может быть пустым");
                throw new ArgumentException("Путь к файлу не может быть пустым.");
            }

            if (!File.Exists(filePath))
            {
                _logger.Error($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }

            if (nodes == null || nodes.Count == 0)
            {
                _logger.Error("Список узлов пуст");
                throw new ArgumentException("Список узлов пуст.");
            }

            if (elements == null || elements.Count == 0)
            {
                _logger.Error("Список элементов пуст");
                throw new ArgumentException("Список элементов пуст.");
            }

            // Проверяем сопоставления
            int nodesWithMapping = nodes.Count(n => n.AppropriateLiraNode.Id != 0);
            int elementsWithMapping = elements.Count(e => e.AppropriateLiraElement.Id != 0);

            _logger.Info($"Узлов с сопоставлением: {nodesWithMapping}/{nodes.Count}");
            _logger.Info($"Элементов с сопоставлением: {elementsWithMapping}/{elements.Count}");

            if (nodesWithMapping == 0)
            {
                _logger.Warning("Нет сопоставленных узлов - возможно ошибка в данных");
            }

            _logger.EndOperation("Валидация входных данных");
        }

        private void CreateBackup(string originalFilePath)
        {
            try
            {
                string backupPath = $"{originalFilePath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(originalFilePath, backupPath, true);
                _logger.Info($"Создан backup: {backupPath}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Не удалось создать backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерирует отчет о записанных данных
        /// </summary>
        public string GenerateReport(List<MidasNodeInfo> nodes, List<MidasElementInfo> elements)
        {
            var report = new StringBuilder();
            report.AppendLine("=== ОТЧЕТ О ЗАПИСИ ДАННЫХ В ЛИРА-САПР ===");
            report.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // Узлы и жесткости
            var nodesWithRigidity = nodes.Where(n => n.RigidityNumber > 0).ToList();
            report.AppendLine("1. УЗЛЫ И ЖЕСТКОСТИ:");
            report.AppendLine($"   Всего узлов: {nodes.Count}");
            report.AppendLine($"   Узлов с жесткостями: {nodesWithRigidity.Count}");
            report.AppendLine($"   Узлов без сопоставления: {nodes.Count(n => n.AppropriateLiraNode.Id == 0)}");

            if (nodesWithRigidity.Any())
            {
                report.AppendLine($"   Номера жесткостей: {nodesWithRigidity.Min(n => n.RigidityNumber)} - " +
                                 $"{nodesWithRigidity.Max(n => n.RigidityNumber)}");
                report.AppendLine($"   Диапазон значений жесткостей: " +
                                 $"{nodesWithRigidity.Min(n => n.Plaque?.rigidNodes ?? 0):F4} - " +
                                 $"{nodesWithRigidity.Max(n => n.Plaque?.rigidNodes ?? 0):F4}");
            }

            // Элементы и коэффициенты
            var elementsWithCoefficient = elements.Where(e => e.BeddingCoefficient > 0).ToList();
            report.AppendLine();
            report.AppendLine("2. ЭЛЕМЕНТЫ И КОЭФФИЦИЕНТЫ ПОСТЕЛИ:");
            report.AppendLine($"   Всего элементов: {elements.Count}");
            report.AppendLine($"   Элементов с коэффициентами: {elementsWithCoefficient.Count}");
            report.AppendLine($"   Элементов без сопоставления: {elements.Count(e => e.AppropriateLiraElement.Id == 0)}");

            if (elementsWithCoefficient.Any())
            {
                report.AppendLine($"   Диапазон коэффициентов: {elementsWithCoefficient.Min(e => e.BeddingCoefficient):F3} - " +
                                 $"{elementsWithCoefficient.Max(e => e.BeddingCoefficient):F3}");
                report.AppendLine($"   Средний коэффициент: {elementsWithCoefficient.Average(e => e.BeddingCoefficient):F3}");
            }

            return report.ToString();
        }
    }
}