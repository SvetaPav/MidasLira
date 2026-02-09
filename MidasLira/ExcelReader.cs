using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using static MidasLira.Mapper;

namespace MidasLira
{

    public class StressInfo
    {
        public int[] NodeIds { get; set; } = [];  // Новый синтаксис вместо Array.Empty<int>()
        public double StressValue { get; set; } = 0;
    }

    // добавить проверку на количество узлов и элементов
    public class ExcelReader
    {
        private readonly Logger _logger;

        // Можно передать внешний логгер или использовать глобальный AppLogger
        public ExcelReader(Logger? logger = null)
        {
            _logger = logger ?? new Logger(false); // false - не выводить в консоль каждый раз
        }

        /// <summary>
        /// Главный метод чтения данных из файла Excel.
        /// </summary>
        public (List<MidasNodeInfo> midasNodes,
                List<LiraNodeInfo> liraNodes,
                List<MidasElementInfo> midasElements,
                List<LiraElementInfo> liraElements)
            ReadFromExcel(string path)
        {
            ValidateFilePath(path);

            ExcelPackage.License.SetNonCommercialPersonal("Svetlana");
            using var package = new ExcelPackage(new FileInfo(path));

            ValidateRequiredSheets(package);

            _logger.Info($"Начало чтения Excel-файла: {path}");

            // 1. Чтение узлов
            var midasNodes = ReadMidasNodes(package);
            var liraNodes = ReadLiraNodes(package);

            _logger.Info($"Прочитано узлов: MIDAS = {midasNodes.Count}, ЛИРА = {liraNodes.Count}");

            // 2. Чтение элементов
            var midasElements = ReadMidasElements(package, midasNodes);
            var liraElements = ReadLiraElements(package, liraNodes);

            _logger.Info($"Прочитано элементов: MIDAS = {midasElements.Count}, ЛИРА = {liraElements.Count}");

            // 3. Привязка элементов к узлам (заполняем node.Elements)
            AttachElementsToNodes(midasNodes, midasElements);
            AttachElementsToNodes(liraNodes, liraElements);

            // 4. Чтение напряжений и привязка к элементам MIDAS
            var stresses = ReadStresses(package);
            AttachStressesToMidasElements(midasElements, stresses, midasNodes);

            return (midasNodes, liraNodes, midasElements, liraElements);
        }

        #region Валидация и вспомогательные методы

        private static void ValidateFilePath(string path)
        {
            // ПРОВЕРКА: Путь к файлу
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу Excel не может быть пустым.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");
        }

        private static void ValidateRequiredSheets(ExcelPackage package)
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
        }

        /// <summary>
        /// Определяет последнюю строку с данными в указанном столбце.
        /// </summary>
        private static int GetLastRowWithData(ExcelWorksheet worksheet, int column = 1)
        {
            if (worksheet.Dimension == null) return 0;

            var lastRow = worksheet.Dimension.End.Row;
            // Идём снизу вверх, пока не найдём непустую ячейку в заданной колонке
            for (int row = lastRow; row >= worksheet.Dimension.Start.Row; row--)
            {
                var cellValue = worksheet.Cells[row, column].Value;
                if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                    return row;
            }
            return 0;
        }

        /// <summary>
        /// Безопасно создаёт словарь узлов, пропуская дубликаты и некорректные ID.
        /// </summary>
        private Dictionary<int, TNode> CreateNodeDictionarySafe<TNode>(
            IEnumerable<TNode> nodes,
            Func<TNode, int> idSelector,
            string nodeType)
        {
            var dict = new Dictionary<int, TNode>();
            foreach (var node in nodes)
            {
                int id = idSelector(node);
                if (id <= 0)
                {
                    _logger.Warning($"{nodeType}: узел с некорректным ID = {id} пропущен");
                    continue;
                }

                if (!dict.TryAdd(id, node))
                {
                    _logger.Warning($"{nodeType}: обнаружен дубликат узла ID = {id}. Первое вхождение будет использовано.");
                }
               
            }
            return dict;
        }

        #endregion


        #region Чтение узлов

        private List<MidasNodeInfo> ReadMidasNodes(ExcelPackage package)
        {
            var worksheet = package.Workbook.Worksheets["Sheet1"];
            var lastRow = GetLastRowWithData(worksheet, 1);

            if (lastRow < 2)
                throw new InvalidDataException("Лист 'Sheet1' не содержит данных для узлов MIDAS.");

            var nodes = new List<MidasNodeInfo>(lastRow - 1);
            for (int row = 2; row <= lastRow; row++)
            {
                int id = worksheet.Cells[row, 1].GetValue<int>();
                if (id <= 0)
                {
                    _logger.Warning($"Sheet1 строка {row}: ID узла MIDAS = {id} <= 0, строка пропущена");
                    continue;
                }

                double x = worksheet.Cells[row, 2].GetValue<double>();
                double y = worksheet.Cells[row, 3].GetValue<double>();
                double z = worksheet.Cells[row, 4].GetValue<double>();
                double displacement = worksheet.Cells[row, 5].GetValue<double>();

                nodes.Add(new MidasNodeInfo(id, x, y, z, displacement, null));
            }

            _logger.Debug($"Прочитано узлов MIDAS: {nodes.Count}");
            return nodes;
        }

        private List<LiraNodeInfo> ReadLiraNodes(ExcelPackage package)
        {
            var worksheet = package.Workbook.Worksheets["Sheet2"];
            var lastRow = GetLastRowWithData(worksheet, 1);

            if (lastRow < 2)
                throw new InvalidDataException("Лист 'Sheet2' не содержит данных для узлов ЛИРА-САПР.");

            var nodes = new List<LiraNodeInfo>(lastRow - 1);
            for (int row = 2; row <= lastRow; row++)
            {
                int id = worksheet.Cells[row, 1].GetValue<int>();
                if (id <= 0)
                {
                    _logger.Warning($"Sheet2 строка {row}: ID узла ЛИРА = {id} <= 0, строка пропущена");
                    continue;
                }

                double x = worksheet.Cells[row, 2].GetValue<double>();
                double y = worksheet.Cells[row, 3].GetValue<double>();
                double z = worksheet.Cells[row, 4].GetValue<double>();

                nodes.Add(new LiraNodeInfo(id, x, y, z, []));
            }

            _logger.Debug($"Прочитано узлов ЛИРА: {nodes.Count}");
            return nodes;
        }

        #endregion


        #region Чтение элементов

        private List<MidasElementInfo> ReadMidasElements(ExcelPackage package, List<MidasNodeInfo> midasNodes)
        {
            var worksheet = package.Workbook.Worksheets["Sheet3"];
            var lastRow = GetLastRowWithData(worksheet, 1);

            if (lastRow < 2)
                throw new InvalidDataException("Лист 'Sheet3' не содержит данных для элементов MIDAS.");

            var elements = new List<MidasElementInfo>(lastRow - 1);
            var nodeDict = CreateNodeDictionarySafe(midasNodes, n => n.Id, "MIDAS");

            for (int row = 2; row <= lastRow; row++)
            {
                int id = worksheet.Cells[row, 1].GetValue<int>();
                if (id <= 0)
                {
                    _logger.Warning($"Sheet3 строка {row}: ID элемента MIDAS = {id} <= 0, строка пропущена");
                    continue;
                }

                // Собираем узлы элемента (максимум 4, удаляем нулевые)
                var nodeIds = new[]
                {
                    worksheet.Cells[row, 2].GetValue<int>(),
                    worksheet.Cells[row, 3].GetValue<int>(),
                    worksheet.Cells[row, 4].GetValue<int>(),
                    worksheet.Cells[row, 5].GetValue<int>()
                }.Where(idVal => idVal > 0).ToArray();

                if (nodeIds.Length < 3)
                {
                    _logger.Warning($"Sheet3 строка {row}: элемент ID={id} имеет менее 3 валидных узлов. Пропущен.");
                    continue;
                }

                // Вычисляем среднее перемещение узлов элемента
                double displacement = nodeIds
                    .Where(nodeDict.ContainsKey)
                    .Select(nodeId => nodeDict[nodeId].NodeDisplacement)
                    .DefaultIfEmpty(0)
                    .Average();

                elements.Add(new MidasElementInfo(
                    id,
                    nodeIds,
                    0,           // Stress – будет заполнено позже
                    displacement,
                    0));         // BeddingCoefficient – будет заполнено позже
            }

            _logger.Debug($"Прочитано элементов MIDAS: {elements.Count}");
            return elements;
        }

        private List<LiraElementInfo> ReadLiraElements(ExcelPackage package, List<LiraNodeInfo> liraNodes)
        {
            var worksheet = package.Workbook.Worksheets["Sheet4"];
            var lastRow = GetLastRowWithData(worksheet, 1);

            if (lastRow < 2)
                throw new InvalidDataException("Лист 'Sheet4' не содержит данных для элементов ЛИРА-САПР.");

            var elements = new List<LiraElementInfo>(lastRow - 1);
            var nodeDict = CreateNodeDictionarySafe(liraNodes, n => n.Id, "ЛИРА");

            for (int row = 2; row <= lastRow; row++)
            {
                int id = worksheet.Cells[row, 1].GetValue<int>();
                if (id <= 0)
                {
                    _logger.Warning($"Sheet4 строка {row}: ID элемента ЛИРА = {id} <= 0, строка пропущена");
                    continue;
                }

                var nodeIds = new[]
                {
                    worksheet.Cells[row, 2].GetValue<int>(),
                    worksheet.Cells[row, 3].GetValue<int>(),
                    worksheet.Cells[row, 4].GetValue<int>(),
                    worksheet.Cells[row, 5].GetValue<int>()
                }.Where(idVal => idVal > 0).ToArray();

                if (nodeIds.Length < 3)
                {
                    _logger.Warning($"Sheet4 строка {row}: элемент ЛИРА ID={id} имеет менее 3 валидных узлов. Пропущен.");
                    continue;
                }

                // Проверяем, что все узлы элемента существуют в словаре узлов ЛИРА
                var missingNodes = nodeIds.Where(nid => !nodeDict.ContainsKey(nid)).ToArray();
                if (missingNodes.Length != 0)
                {
                    _logger.Warning($"Sheet4 строка {row}: элемент ЛИРА ID={id} ссылается на несуществующие узлы: {string.Join(",", missingNodes)}. Элемент пропущен.");
                    continue;
                }

                elements.Add(new LiraElementInfo(id, nodeIds));
            }

            _logger.Debug($"Прочитано элементов ЛИРА: {elements.Count}");
            return elements;
        }

        #endregion



        #region Привязка элементов к узлам

        private void AttachElementsToNodes(List<MidasNodeInfo> nodes, List<MidasElementInfo> elements)
        {
            var dict = CreateNodeDictionarySafe(nodes, n => n.Id, "MIDAS");
            foreach (var element in elements)
            {
                foreach (var nodeId in element.NodeIds)
                {
                    if (dict.TryGetValue(nodeId, out var node))
                        node.Elements.Add(element);
                    else
                        _logger.Warning($"Привязка MIDAS: узел {nodeId} для элемента {element.Id} не найден");
                }
            }
        }

        private void AttachElementsToNodes(List<LiraNodeInfo> nodes, List<LiraElementInfo> elements)
        {
            var dict = CreateNodeDictionarySafe(nodes, n => n.Id, "ЛИРА");
            foreach (var element in elements)
            {
                foreach (var nodeId in element.NodeIds)
                {
                    if (dict.TryGetValue(nodeId, out var node))
                        node.Elements.Add(element);
                    else
                        _logger.Warning($"Привязка ЛИРА: узел {nodeId} для элемента {element.Id} не найден");
                }
            }
        }

        #endregion


        #region Чтение напряжений и привязка

        private Dictionary<int, StressInfo> ReadStresses(ExcelPackage package)
        {
            var worksheet = package.Workbook.Worksheets["Sheet5"];
            var lastRow = GetLastRowWithData(worksheet, 1);

            if (lastRow < 2)
                throw new InvalidDataException("Лист 'Sheet5' не содержит данных о напряжениях.");

            var stresses = new Dictionary<int, StressInfo>();
            for (int row = 2; row <= lastRow; row++)
            {
                int elementId = worksheet.Cells[row, 1].GetValue<int>();
                if (elementId <= 0)
                {
                    _logger.Warning($"Sheet5 строка {row}: ID объёмного элемента = {elementId} <= 0, строка пропущена");
                    continue;
                }

                var nodeIds = new[]
                {
                    worksheet.Cells[row, 2].GetValue<int>(),
                    worksheet.Cells[row, 3].GetValue<int>(),
                    worksheet.Cells[row, 4].GetValue<int>(),
                    worksheet.Cells[row, 5].GetValue<int>(),
                    worksheet.Cells[row, 6].GetValue<int>(),
                    worksheet.Cells[row, 7].GetValue<int>(),
                    worksheet.Cells[row, 8].GetValue<int>(),
                    worksheet.Cells[row, 9].GetValue<int>()
                }.Where(id => id > 0).ToArray();

                if (nodeIds.Length < 6)
                {
                    _logger.Warning($"Sheet5 строка {row}: элемент {elementId} имеет менее 6 узлов, пропущен");
                    continue;
                }

                double stressValue = worksheet.Cells[row, 10].GetValue<double>();

                if (stresses.ContainsKey(elementId))
                {
                    _logger.Warning($"Sheet5: дублирующийся ID объёмного элемента {elementId}, использовано первое вхождение");
                    continue;
                }

                stresses[elementId] = new StressInfo
                {
                    NodeIds = nodeIds,
                    StressValue = stressValue
                };
            }

            _logger.Debug($"Прочитано напряжений для {stresses.Count} объёмных элементов");
            return stresses;
        }

        private void AttachStressesToMidasElements(
            List<MidasElementInfo> midasElements,
            Dictionary<int, StressInfo> stresses,
            List<MidasNodeInfo> midasNodes)
        {
            var nodeDict = CreateNodeDictionarySafe(midasNodes, n => n.Id, "MIDAS");
            int assignedCount = 0;

            foreach (var element in midasElements)
            {
                // Ищем объёмный элемент, имеющий не менее 3 общих узлов с элементом MIDAS
                var matchingStress = stresses.Values.FirstOrDefault(s =>
                    s.NodeIds.Intersect(element.NodeIds).Count() >= 3);

                if (matchingStress != null)
                {
                    element.Stress = matchingStress.StressValue;
                    if (Math.Abs(element.Displacement) > 1e-12)
                        element.BeddingCoefficient = element.Stress / element.Displacement;
                    else
                        _logger.Warning($"Элемент MIDAS ID={element.Id}: перемещение равно нулю, коэффициент постели не вычислен");
                    assignedCount++;
                }
                else
                {
                    _logger.Debug($"Для элемента MIDAS ID={element.Id} не найден подходящий объёмный элемент (общих узлов < 3)");
                }
            }

            _logger.Info($"Назначены напряжения для {assignedCount} элементов MIDAS из {midasElements.Count}");
        }

        #endregion
    }

}


