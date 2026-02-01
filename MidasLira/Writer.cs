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
    public class Writer //переписать
    {
        private readonly PositionFinder _positionFinder;

        public Writer(PositionFinder positionFinder)
        {
            _positionFinder = positionFinder;
        }

        /// <summary>
        /// Записывает данные в файл ЛИРА-САПР. 
        /// </summary>
        public void WriteNodeAndBeddingData(string filePath, List<MidasNodeInfo> nodes, List<MidasElementInfo> elements, List<Plaque> plaques)
        {
            // ПРОВЕРКА: Входные данные
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Целевой файл не найден: {filePath}");
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (plaques == null) throw new ArgumentNullException(nameof(plaques));

            // Находим позиции для вставки данных
            var positions = _positionFinder.ParseTextFile(filePath);

            // Читаем исходный файл целиком
            string[] lines = File.ReadAllLines(filePath);

            // Находим максимальное существующее значение жесткости
            int maxRigidityNumber = FindMaxRigidityNumber(lines);

            // Формируем новые строки для вставки
            var newStiffnessLines = CreateStiffnessSection(nodes);
            var newNodeMappingLines = CreateElement56Section(nodes, maxRigidityNumber);
            var newBeddingCoeffLines = CreateBeddingCoefficientSection(elements);

            // Формируем новый файл
            var newContent = new List<string>(lines);

            // Вставляем данные о жесткостях узлов в раздел (3/)
            if (positions.Section3EndPosition != -1)
            {
                newContent.InsertRange(positions.Section3EndPosition, newStiffnessLines);
            }

            // Вставляем данные о КЭ56 в раздел (1/)
            if (positions.Section1EndPosition != -1)
            {
                newContent.InsertRange(positions.Section1EndPosition, newNodeMappingLines);
            }

            // Вставляем данные о коэффициентах постели после последней строки файла
            if (positions.LastLinePosition != -1)
            {
                newContent.InsertRange(positions.LastLinePosition + 1, newBeddingCoeffLines);
            }

            // Записываем изменённый файл
            File.WriteAllLines(filePath, newContent);
        }

        // Метод для создания строки с жесткостями 
        private List<string> CreateStiffnessSection(List<MidasNodeInfo> nodes)  
        {
            var content = new List<string>();
            // Сортировка узлов по полю AppropriateLiraNode.Id
            var sortedNodes = nodes.OrderBy(node => node.AppropriateLiraNode.Id).ToList();

            foreach (var node in nodes)
            {
                content.Add($"{node.AppropriateLiraNode.Id} {node.Plaque.rigidNodes:F4} {node.Plaque.rigidNodes:F4} 0 0 0 0 /");
            }
            return content;
        }

        // Метод для создания строки с КЭ56
        private List<string> CreateElement56Section(List<MidasNodeInfo> nodes, int RigidityNumber)  
        {
            var content = new List<string>();
            // Сортировка узлов по полю AppropriateLiraNode.Id
            var sortedNodes = nodes.OrderBy(node => node.AppropriateLiraNode.Id).ToList();

            foreach (var node in nodes)
            {
                content.Add($"56 {RigidityNumber} {node.AppropriateLiraNode.Id} /");
                RigidityNumber++;
            }
            return content;
        }

        // Метод для создания строки с коэффициентами постели
        private List<string> CreateBeddingCoefficientSection(List<MidasElementInfo> elements)  
        {
            var content = new List<string> { "(19/\n" };
            // Сортировка 'элементов по полю AppropriateLiraElement.Id
            var sortedElements = elements.OrderBy(element => element.AppropriateLiraElement.Id).ToList();

            foreach (var element in elements)
            {
                content.Add($"{element.AppropriateLiraElement.Id} {element.BeddingCoefficient:F3} 0 0 0 /\n");
            }
            return content;
        }

        // Метод для поиска максимального номера жесткости
        private int FindMaxRigidityNumber(string[] lines)
        {
            int maxRigidityNumber = 0;
            foreach (var line in lines)
            {
                if (line.Contains("(3/"))
                {
                    foreach (var part in line.Split('/'))
                    {
                        if (part.Contains(' '))
                        {
                            var values = part.Split(' ');
                            if (values.Length > 0 && int.TryParse(values[0], out int rigidityNumber))
                            {
                                maxRigidityNumber = Math.Max(maxRigidityNumber, rigidityNumber);
                            }
                        }
                    }
                }
            }
            return maxRigidityNumber;
        }
    }
}
