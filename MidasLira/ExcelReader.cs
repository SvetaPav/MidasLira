using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MidasLira.Mapper;

namespace MidasLira
{
    public class StressInfo
    {
        public int[] NodeIds { get; set; }
        public double StressValue { get; set; }
    }

    // добавить проверку на количество узлов и элементов
    public class ExcelReader
    {
        /// <summary>
        /// Чтение данных из файла Excel.
        /// </summary>
        public (List<MidasNodeInfo> midasNodes, List<LiraNodeInfo> liraNodes, List<MidasElementInfo> ElementsMidas, List<LiraElementInfo> ElementsLira) ReadFromExcel(string path)

        {
            // ПРОВЕРКА: Путь к файлу
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу Excel не может быть пустым.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");


            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                if (package.Workbook.Worksheets.Count == 0)
                    throw new InvalidOperationException("Файл Excel не содержит ни одного листа.");

                // ПРОВЕРКА: Существование обязательных листов
                var requiredSheets = new[] { "Sheet1", "Sheet2", "Sheet3", "Sheet4", "Sheet5" };
                foreach (var sheetName in requiredSheets)
                {
                    if (package.Workbook.Worksheets[sheetName] == null)
                        throw new InvalidOperationException($"В файле отсутствует обязательный лист: '{sheetName}'.");
                }


                // Лист с узлами MIDAS
                var worksheet = package.Workbook.Worksheets["Sheet1"];
                // ПРОВЕРКА: Данные на листе
                if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
                    throw new InvalidDataException("Лист 'Sheet1' не содержит данных для узлов MIDAS.");

                var rowsCount = worksheet.Dimension.Rows;

                // Читаем узлы MIDAS
                var nodesMidas = new List<MidasNodeInfo>(rowsCount - 1); // Исключаем заголовочную строку
                for (int row = 2; row <= rowsCount; row++)
                {
                    nodesMidas.Add(new MidasNodeInfo(
                        worksheet.Cells[row, 1].GetValue<int>(), // Id
                        worksheet.Cells[row, 2].GetValue<double>(), // X
                        worksheet.Cells[row, 3].GetValue<double>(), // Y
                        worksheet.Cells[row, 4].GetValue<double>(), // Z
                        worksheet.Cells[row, 5].GetValue<double>(), // NodeDisplacement
                        new List<MidasElementInfo>()));                 // Пока элементы пусты
                }

                // Лист с узлами ЛИРА-САПР
                var liraWorksheet = package.Workbook.Worksheets["Sheet2"];
                var liraRowsCount = liraWorksheet.Dimension.Rows;

                // Читаем узлы ЛИРА-САПР
                var nodesLira = new List<LiraNodeInfo>(liraRowsCount - 1);
                for (int row = 2; row <= liraRowsCount; row++)
                {
                    nodesLira.Add(new LiraNodeInfo(
                        liraWorksheet.Cells[row, 1].GetValue<int>(), // Id
                        liraWorksheet.Cells[row, 2].GetValue<double>(), // X
                        liraWorksheet.Cells[row, 3].GetValue<double>(), // Y
                        liraWorksheet.Cells[row, 4].GetValue<double>(), // Z
                        new List<LiraElementInfo>()));                     // Пока элементы пусты
                }

                // Лист с элементами MIDAS
                var elementsWorksheet = package.Workbook.Worksheets["Sheet3"];
                var elementsRowsCount = elementsWorksheet.Dimension.Rows;

                // Читаем элементы MIDAS
                var elementsMidas = new List<MidasElementInfo>(elementsRowsCount - 1);
                for (int row = 2; row <= elementsRowsCount; row++)
                {
                    // Узлы хранятся в соседних ячейках (например, в колонках B, C, D)
                    var nodeIds = new[]
                    {
                        elementsWorksheet.Cells[row, 2].GetValue<int>(), // Первый узел
                        elementsWorksheet.Cells[row, 3].GetValue<int>(), // Второй узел
                        elementsWorksheet.Cells[row, 4].GetValue<int>(), // Третий узел
                        elementsWorksheet.Cells[row, 5].GetValue<int>()  // Четвертый узел (если элемент четырехугольный)
                    }.Where(id => id > 0).ToArray(); // Убираем нулевые значения, если элемент трехугольный

                    // Вычисляем среднее арифметическое перемещений узлов, принадлежащих элементу
                    var displacement = nodesMidas.Where(n => nodeIds.Contains(n.Id))
                                               .Select(n => n.NodeDisplacement)
                                               .Average();

                    elementsMidas.Add(new MidasElementInfo(
                        elementsWorksheet.Cells[row, 1].GetValue<int>(), // Id
                        nodeIds,                                         // Узлы элемента
                        0, // Stress, заполняется позже
                        displacement, // Displacement
                        0)); // С1, заполняется позже
                }

                // Привязываем элементы к узлам
                foreach (var element in elementsMidas)
                {
                    foreach (var nodeId in element.NodeIds)
                    {
                        // Ищем узел по его уникальному идентификатору
                        var node = nodesMidas.FirstOrDefault(n => n.Id == nodeId);

                        // Присоединяем элемент к узлу, если узел найден
                        if (node != null) // Проверка на положительный идентификатор узла
                        {
                            node.Elements.Add(element);
                        }
                    }
                }

                // Лист с элементами Lira
                var elementsLiraWorksheet = package.Workbook.Worksheets["Sheet4"];
                var elementsLiraRowsCount = elementsLiraWorksheet.Dimension.Rows;


                // Читаем элементы Lira
                var elementsLira = new List<LiraElementInfo>(elementsLiraRowsCount - 1);
                for (int row = 2; row <= elementsLiraRowsCount; row++)
                {
                    // Узлы хранятся в соседних ячейках (например, в колонках B, C, D)
                    var nodeIds = new[]
                    {
                        elementsLiraWorksheet.Cells[row, 2].GetValue<int>(), // Первый узел
                        elementsLiraWorksheet.Cells[row, 3].GetValue<int>(), // Второй узел
                        elementsLiraWorksheet.Cells[row, 4].GetValue<int>(), // Третий узел
                        elementsLiraWorksheet.Cells[row, 5].GetValue<int>()  // Четвертый узел (если элемент четырехугольный)
                    }.Where(id => id > 0).ToArray(); // Убираем нулевые значения, если элемент трехугольный

                    elementsLira.Add(new LiraElementInfo(
                        elementsLiraWorksheet.Cells[row, 1].GetValue<int>(), // Id
                        nodeIds)                 // Узлы элемента
                        );
                }


                foreach (var element in elementsLira)
                {
                    foreach (var nodeId in element.NodeIds)
                    {
                        // Ищем узел по его уникальному идентификатору
                        var node = nodesLira.FirstOrDefault(n => n.Id == nodeId);

                        // Присоединяем элемент к узлу, если узел найден
                        if (node.Id != 0) // Проверка на положительный идентификатор узла
                        {
                            node.Elements.Add(element);
                        }
                    }
                }

                // Лист с напряжениями
                var stressWorksheet = package.Workbook.Worksheets["Sheet5"];
                var stressRowsCount = stressWorksheet.Dimension.Rows;

                // Читаем напряжения
                var stresses = new Dictionary<int, StressInfo>();
                for (int row = 2; row <= stressRowsCount; row++)
                {
                    var elementId = stressWorksheet.Cells[row, 1].GetValue<int>(); // Номер объемного элемента
                    var nodeIds = new[]
                    {
                    stressWorksheet.Cells[row, 2].GetValue<int>(), // Первый узел
                    stressWorksheet.Cells[row, 3].GetValue<int>(), // Второй узел
                    stressWorksheet.Cells[row, 4].GetValue<int>(), // Третий узел
                    stressWorksheet.Cells[row, 5].GetValue<int>(), // Четвертый узел
                    stressWorksheet.Cells[row, 6].GetValue<int>(), // Пятый узел
                    stressWorksheet.Cells[row, 7].GetValue<int>(), // Шестой узел
                    stressWorksheet.Cells[row, 8].GetValue<int>(), // Седьмой узел
                    stressWorksheet.Cells[row, 9].GetValue<int>()  // Восьмой узел
                }.Where(id => id > 0).ToArray(); // Убираем нулевые значения

                    var stressValue = stressWorksheet.Cells[row, 10].GetValue<double>(); // Значение напряжения

                    stresses[elementId] = new StressInfo
                    {
                        NodeIds = nodeIds,
                        StressValue = stressValue
                    };
                }

                // Запись напряжений в элементы Midas
                foreach (var element in elementsMidas)
                {
                    // Находим объемный элемент, содержащий хотя бы 3 общих узла с элементом Midas
                    var matchingStress = stresses.Values.FirstOrDefault(s => s.NodeIds.Intersect(element.NodeIds).Count() >= 3);

                    if (matchingStress != null)
                    {
                        element.Stress = matchingStress.StressValue;
                        element.BeddingCoefficient = element.Stress / element.Displacement;
                    }
                }

                return (nodesMidas, nodesLira, elementsMidas, elementsLira);
            }
        }
    }
}