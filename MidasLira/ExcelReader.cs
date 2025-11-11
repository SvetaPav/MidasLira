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
    public class ExcelReader
    {
        /// <summary>
        /// Чтение данных из файла Excel.
        /// </summary>
        public (List<MidasNodeInfo> midasNodes, List<LiraNodeInfo> liraNodes, List<MidasElementInfo> ElementsMidas, List<LiraElementInfo> ElementsLira) ReadFromExcel(string path)

        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                // Лист с узлами MIDAS
                var worksheet = package.Workbook.Worksheets["Sheet1"];
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

                    elementsMidas.Add(new MidasElementInfo(
                        elementsWorksheet.Cells[row, 1].GetValue<int>(), // Id
                        nodeIds,                                        // Узлы элемента
                        elementsWorksheet.Cells[row, 6].GetValue<double>(), // Stress
                        elementsWorksheet.Cells[row, 7].GetValue<double>(), // Displacement
                        elementsWorksheet.Cells[row, 6].GetValue<double>() / elementsWorksheet.Cells[row, 7].GetValue<double>()));
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

                // Читаем элементы Lira
                var elementsLira = new List<LiraElementInfo>(elementsRowsCount - 1);
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

                    elementsLira.Add(new LiraElementInfo(
                        elementsWorksheet.Cells[row, 1].GetValue<int>(), // Id
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

                return (nodesMidas, nodesLira, elementsMidas, elementsLira);
            }
        }
    }
}
