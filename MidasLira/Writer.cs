using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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
            _logger = logger;
        }

        /// <summary>
        /// Записывает данные в файл ЛИРА-САПР. 
        /// </summary>
        public void WriteNodeAndBeddingData(string filePath, List<MidasNodeInfo> nodes, List<MidasElementInfo> elements, List<Plaque> plaques)
        {
            try 
            {
                // ВАЛИДАЦИЯ ВХОДНЫХ ДАННЫХ
                ValidateInputData(filePath, nodes, elements, plaques);

                // Находим позиции для вставки данных
                var positions = _positionFinder.ParseTextFile(filePath);

                // Чтение файла
                string[] originalLines = File.ReadAllLines(filePath);

                // Проверка существующих разделов
                var positions = _positionFinder.ParseTextFile(filePath);
                ValidateFileSections(positions, originalLines);

                // Находим максимальный номер жесткости ВО ВСЁМ разделе (3/)
                int maxRigidityNumber = FindMaxRigidityNumberInSection(originalLines, positions.Section3StartPosition);

                // Формируем новые данные
                var newStiffnessLines = CreateStiffnessSection(nodes, plaques);
            var newNodeMappingLines = CreateElement56Section(nodes, maxRigidityNumber+1);
            var newBeddingCoeffLines = CreateBeddingCoefficientSection(elements);

                // Вставка с проверкой на переполнение разделов
                var newContent = InsertDataSafely(originalLines.ToList(), positions,
                                                 newStiffnessLines, newNodeMappingLines, newBeddingCoeffLines);

                // Создаем backup перед записью
                CreateBackup(filePath);

                // Запись
                File.WriteAllLines(filePath, newContent);

                _logger?.LogEvent("INFO", $"Данные успешно записаны в {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogEvent("ERROR", $"Ошибка записи в {filePath}: {ex.Message}");
                throw new InvalidOperationException($"Ошибка записи в файл ЛИРА-САПР: {ex.Message}", ex);
            }
        }

        private void ValidateInputData(string filePath, List<MidasNodeInfo> nodes,
                                    List<MidasElementInfo> elements, List<Plaque> plaques)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл не найден: {filePath}");

            if (nodes == null || nodes.Count == 0)
                throw new ArgumentException("Список узлов пуст.");

            // Проверка, что у всех узлов есть сопоставленные узлы ЛИРА-САПР
            var nodesWithoutMapping = nodes.Where(n => n.AppropriateLiraNode.Id == 0).ToList();
            if (nodesWithoutMapping.Count > 0)
                throw new InvalidOperationException(
                    $"Найдено {nodesWithoutMapping.Count} узлов без сопоставления с ЛИРА-САПР");
        }

        private void ValidateFileSections((int Section1End, int Section3End, int LastLine) positions,
                                         string[] lines)
        {
            if (positions.Section1End == -1)
                throw new InvalidDataException("Не найден раздел (1/) в файле ЛИРА-САПР");

            if (positions.Section3End == -1)
                throw new InvalidDataException("Не найден раздел (3/) в файле ЛИРА-САПР");

            // Проверка, что разделы не слишком заполнены
            CheckSectionCapacity(lines, positions.Section1End, "Раздел (1/)");
            CheckSectionCapacity(lines, positions.Section3End, "Раздел (3/)");
        }

        private void CheckSectionCapacity(string[] lines, int sectionEnd, string sectionName)
        {
            // Если до конца файла меньше 5 строк - предупреждение
            if (lines.Length - sectionEnd < 5)
            {
                _logger?.LogEvent("WARNING",
                    $"{sectionName} близок к заполнению. Осталось {lines.Length - sectionEnd} строк.");
            }
        }


        // Метод для создания строки с жесткостями 
        private List<string> CreateStiffnessSection(List<MidasNodeInfo> nodes, List<Plaque> plaques)
        {
            var content = new List<string>();

            // СОРТИРОВКА по ID узла ЛИРА-САПР
            var sortedNodes = nodes
                .Where(n => n.AppropriateLiraNode.Id != 0) // Только с сопоставлением
                .OrderBy(n => n.AppropriateLiraNode.Id)
                .ToList();

            foreach (var node in sortedNodes)
            {
                // Находим плиту для узла
                var plaque = plaques.FirstOrDefault(p => p.Nodes.Contains(node));
                double rigidity = plaque?.rigidNodes ?? 0;

                content.Add($"{node.AppropriateLiraNode.Id} {rigidity:F4} {rigidity:F4} 0 0 0 0 /");
            }

            return content;
        }

        // Метод для создания строки с КЭ56
        private List<string> CreateElement56Section(List<MidasNodeInfo> nodes, int startRigidityNumber)
        {
            var content = new List<string>();
            int currentRigidityNumber = startRigidityNumber;

            // СОРТИРОВКА
            var sortedNodes = nodes
                .Where(n => n.AppropriateLiraNode.Id != 0)
                .OrderBy(n => n.AppropriateLiraNode.Id)
                .ToList();

            foreach (var node in sortedNodes)
            {
                content.Add($"56 {currentRigidityNumber} {node.AppropriateLiraNode.Id} /");
                currentRigidityNumber++;
            }

            return content;
        }

        // Метод для создания строки с коэффициентами постели
        private List<string> CreateBeddingCoefficientSection(List<MidasElementInfo> elements)
        {
            var content = new List<string> { "(19/" };

            // СОРТИРОВКА и фильтрация
            var sortedElements = elements
                .Where(e => e.AppropriateLiraElement.Id != 0 && e.BeddingCoefficient > 0)
                .OrderBy(e => e.AppropriateLiraElement.Id)
                .ToList();

            foreach (var element in sortedElements)
            {
                content.Add($"{element.AppropriateLiraElement.Id} {element.BeddingCoefficient:F3} 0 0 0 /");
            }

            content.Add(""); // Пустая строка в конце раздела
            return content;
        }

        private List<string> InsertDataSafely(List<string> lines,
                                             (int Section1End, int Section3End, int LastLine) positions,
                                             List<string> stiffnessLines,
                                             List<string> element56Lines,
                                             List<string> beddingCoeffLines)
        {
            var result = new List<string>(lines);

            // Вставляем в обратном порядке, чтобы индексы не сбивались
            // 1. Коэффициенты постели в конец файла
            if (beddingCoeffLines.Any())
                result.InsertRange(positions.LastLine + 1, beddingCoeffLines);

            // 2. Жесткости в раздел (3/)
            if (stiffnessLines.Any())
                result.InsertRange(positions.Section3End, stiffnessLines);

            // 3. КЭ56 в раздел (1/)
            if (element56Lines.Any())
                result.InsertRange(positions.Section1End, element56Lines);

            return result;
        }

        private void CreateBackup(string originalFilePath)
        {
            string backupPath = $"{originalFilePath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(originalFilePath, backupPath, true);
            _logger?.LogEvent("INFO", $"Создан backup: {backupPath}");
        }

        // Ищет номера жесткости во всём разделе (3/)
        private int FindMaxRigidityNumberInSection(string[] lines, int sectionStartIndex)
        {
            int maxRigidity = 0;
            bool inSection3 = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Начало раздела (3/)
                if (line.Contains("(3/"))
                {
                    inSection3 = true;
                    continue;
                }

                // Конец раздела (пустая строка)
                if (inSection3 && string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                if (inSection3)
                {
                    // Парсим номер жесткости (первое число в строке)
                    var parts = line.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[0], out int rigidityNum))
                    {
                        maxRigidity = Math.Max(maxRigidity, rigidityNum);
                    }
                }
            }

            return maxRigidity;
        }
    }
}
