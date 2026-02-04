using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Moq;
using Xunit;
using static MidasLira.Mapper;
using MidasLira;

namespace MidasLira.Tests
{
    public class ExcelReaderTests
    {
        private readonly Mock<ExcelPackageWrapper> _packageMock;

        public ExcelReaderTests()
        {
            _packageMock = new Mock<ExcelPackageWrapper>();
        }

        /* Тест проверяет успешное чтение корректно отформатированного Excel файла.
           Файл должен содержать 5 обязательных листа:
           - Sheet1: узлы MIDAS
           - Sheet2: узлы ЛИРА-САПР
           - Sheet3: элементы MIDAS
           - Sheet4: элементы ЛИРА-САПР
           - Sheet5: напряжения

           Проверяемые аспекты:
           1. Все данные корректно считаны из всех листов
           2. Элементы MIDAS привязаны к соответствующим узлам
           3. Напряжения корректно записаны в элементы MIDAS
           4. Коэффициенты постели вычислены
        */
        [Fact]
        public void ReadFromExcel_ValidFile_ReturnsCorrectData()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.midasNodes.Count);
            Assert.Equal(2, result.liraNodes.Count);
            Assert.Equal(1, result.ElementsMidas.Count);
            Assert.Equal(1, result.ElementsLira.Count);
            Assert.Equal(0.02, result.ElementsMidas[0].Displacement);
            Assert.NotNull(result.ElementsMidas[0].BeddingCoefficient);
        }

        /* Тест проверяет валидацию пустого пути к файлу Excel.
           Метод должен выбрасывать ArgumentException с соответствующим сообщением.

           Проверяемые аспекты:
           1. ArgumentException для null или пустого пути
           2. Сообщение содержит описание ошибки
        */
        [Fact]
        public void ReadFromExcel_EmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                ExcelReader.ReadFromExcel(""));

            Assert.Contains("не может быть пустым", exception.Message);
        }

        /* Тест проверяет валидацию отсутствующего файла Excel.
           Метод должен выбрасывать FileNotFoundException с именем файла.

           Проверяемые аспекты:
           1. FileNotFoundException для несуществующего пути
           2. Сообщение содержит имя отсутствующего файла
        */
        [Fact]
        public void ReadFromExcel_FileNotFound_ThrowsFileNotFoundException()
        {
            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() =>
                ExcelReader.ReadFromExcel("nonexistent.xlsx"));

            Assert.Contains("не найден", exception.Message);
            Assert.Contains("nonexistent.xlsx", exception.Message);
        }

        /* Тест проверяет вычисление среднего перемещения узлов элемента.
           Для каждого элемента MIDAS вычисляется среднее арифметическое
           перемещений всех узлов, принадлежащих элементу.

           Проверяемые аспекты:
           1. Перемещение вычисляется корректно для всех узлов элемента
           2. Среднее значение соответствует математическому ожиданию
           3. Перемещение записывается в MidasElementInfo
        */
        [Fact]
        public void ReadFromExcel_CalculatesDisplacementCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(0.025, result.ElementsMidas[0].Displacement); // (0.02 + 0.03) / 2
        }

        /* Тест проверяет привязку элементов MIDAS к соответствующим узлам.
           После чтения элементов, каждый элемент MIDAS добавляется в список
           Elements соответствующих узлов.

           Проверяемые аспекты:
           1. Элемент MIDAS найден в списке узлов
           2. Элемент добавлен в Elements узла
           3. Привязка работает для всех узлов элемента
        */
        [Fact]
        public void ReadFromExcel_BindElementsToNodesCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Contains(result.midasNodes[0].Elements, e => e.Id == result.ElementsMidas[0].Id);
            Assert.Contains(result.midasNodes[1].Elements, e => e.Id == result.ElementsMidas[0].Id);
        }

        /* Тест проверяет чтение узлов MIDAS из Sheet1.
           Лист Sheet1 содержит 5 колонок: Id, X, Y, Z, NodeDisplacement.
           Чтение начинается со 2-й строки (пропуская заголовок).

           Проверяемые аспекты:
           1. Все узлы корректно считаны из Excel
           2. Свойства Id, X, Y, Z, NodeDisplacement заполнены
           3. Список элементов узлов инициализирован пустым
        */
        [Fact]
        public void ReadFromExcel_ReadsMidasNodesCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(2, result.midasNodes.Count);
            Assert.Equal(1, result.midasNodes[0].Id);
            Assert.Equal(0.0, result.midasNodes[0].X);
            Assert.Equal(1.0, result.midasNodes[0].Y);
            Assert.Equal(0.0, result.midasNodes[0].Z);
            Assert.Equal(0.02, result.midasNodes[0].NodeDisplacement);
        }

        /* Тест проверяет чтение узлов ЛИРА-САПР из Sheet2.
           Лист Sheet2 содержит 4 колонки: Id, X, Y, Z.
           Чтение начинается со 2-й строки (пропуская заголовок).

           Проверяемые аспекты:
           1. Все узлы ЛИРА корректно считаны из Excel
           2. Свойства Id, X, Y, Z заполнены
           3. Список элементов узлов инициализирован пустым
        */
        [Fact]
        public void ReadFromExcel_ReadsLiraNodesCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(2, result.liraNodes.Count);
            Assert.Equal(10, result.liraNodes[0].Id);
            Assert.Equal(0.0, result.liraNodes[0].X);
            Assert.Equal(1.0, result.liraNodes[0].Y);
            Assert.Equal(0.0, result.liraNodes[0].Z);
            Assert.Empty(result.liraNodes[0].Elements);
        }

        /* Тест проверяет чтение элементов MIDAS из Sheet3.
           Лист Sheet3 содержит узлы в колонках B, C, D, E (2, 3, 4, 5).
           Элементы могут быть треугольными (3 узла) или четырехугольными (4 узла).

           Проверяемые аспекты:
           1. Элементы MIDAS корректно считаны из Excel
           2. NodeIds массив содержит корректные идентификаторы
           3. Displacement вычислен среднее перемещение
           4. Stress и BeddingCoefficient заполняются позже из Sheet5
        */
        [Fact]
        public void ReadFromExcel_ReadsMidasElementsCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Single(result.ElementsMidas);
            Assert.Equal(1, result.ElementsMidas[0].Id);
            Assert.Equal(2, result.ElementsMidas[0].NodeIds.Length);
            Assert.Equal(0.025, result.ElementsMidas[0].Displacement);
        }

        /* Тест проверяет чтение элементов ЛИРА-САПР из Sheet4.
           Лист Sheet4 содержит узлы в колонках B, C, D, E (2, 3, 4, 5).

           Проверяемые аспекты:
           1. Элементы ЛИРА корректно считаны из Excel
           2. NodeIds массив содержит корректные идентификаторы
           3. Элементы не имеют дополнительных параметров (только Id и NodeIds)
        */
        [Fact]
        public void ReadFromExcel_ReadsLiraElementsCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Single(result.ElementsLira);
            Assert.Equal(100, result.ElementsLira[0].Id);
            Assert.Equal(2, result.ElementsLira[0].NodeIds.Length);
        }

        /* Тест проверяет чтение напряжений из Sheet5.
           Лист Sheet5 содержит информацию о напряжениях элементов:
           - Колонка 1: Id элемента
           - Колонки 2-9: Идентификаторы узлов (до 8 узлов)
           - Колонка 10: Значение напряжения

           Проверяемые аспекты:
           1. Напряжения корректно считаны из Excel
           2. NodeIds массив содержит корректные идентификаторы
           3. StressValue содержит числовое значение
        */
        [Fact]
        public void ReadFromExcel_ReadsStressesCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.NotNull(result.ElementsMidas[0].Stress);
            Assert.Equal(100.5, result.ElementsMidas[0].Stress);
        }

        /* Тест проверяет запись напряжений в элементы MIDAS.
           Напряжения из Sheet5 сопоставляются с элементами MIDAS
           по наличию минимум 3 общих узлов.

           Проверяемые аспекты:
           1. Напряжение записано в соответствующий элемент MIDAS
           2. Коэффициент постели вычислен (Stress/Displacement)
           3. Сопоставление работает при 3+ общих узлах
        */
        [Fact]
        public void ReadFromExcel_AssignsStressesCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(100.5, result.ElementsMidas[0].Stress);
            Assert.Equal(100.5 / 0.025, result.ElementsMidas[0].BeddingCoefficient, 5);
        }

        /* Тест проверяет обработку треугольных элементов.
           Треугольные элементы имеют 3 узла, четвертый узел имеет Id=0
           и должен быть отфильтрован.

           Проверяемые аспекты:
           1. Треугольный элемент имеет 3 узла в NodeIds
           2. Нулевые идентификаторы отфильтрованы
           3. Элемент корректно создан
        */
        [Fact]
        public void ReadFromExcel_HandlesTriangularElements()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(2, result.ElementsMidas[0].NodeIds.Length);
            Assert.All(result.ElementsMidas[0].NodeIds, id => id > 0);
        }

        /* Тест проверяет игнорирование нулевых идентификаторов узлов.
           Для треугольных элементов четвертый узел имеет Id=0
           и должен быть исключен из NodeIds.

           Проверяемые аспекты:
           1. Метод Where(id => id > 0) фильтрует нулевые значения
           2. NodeIds содержит только положительные идентификаторы
           3. Треугольные элементы имеют массив длиной 3
        */
        [Fact]
        public void ReadFromExcel_IgnoresZeroNodeIds()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.All(result.ElementsMidas[0].NodeIds, id => id > 0);
        }

        /* Тест проверяет вычисление коэффициента постели.
           Коэффициент постели C1 вычисляется как отношение напряжения
           к перемещению: BeddingCoefficient = Stress / Displacement.

           Проверяемые аспекты:
           1. Коэффициент вычислен для элементов с напряжениями
           2. Формула вычисления соблюдена (деление)
           3. Значение коэффициента корректно
        */
        [Fact]
        public void ReadFromExcel_CalculatesBeddingCoefficientCorrectly()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(100.5, result.ElementsMidas[0].Stress);
            Assert.Equal(0.025, result.ElementsMidas[0].Displacement);
            double expectedCoefficient = 100.5 / 0.025;
            Assert.Equal(expectedCoefficient, result.ElementsMidas[0].BeddingCoefficient, 5);
        }

        /* Тест проверяет чтение элементов с разным количеством узлов.
           Элементы могут быть трехугольными (3 узла) или
           четырехугольными (4 узла).

           Проверяемые аспекты:
           1. Корректное чтение трехугольных элементов
           2. Корректное чтение четырехугольных элементов
           3. Длина массива NodeIds соответствует типу элемента
        */
        [Fact]
        public void ReadFromExcel_HandlesDifferentElementTypes()
        {
            // Arrange
            var testData = CreateMockPackageData();

            // Act
            var result = ExcelReader.ReadFromExcel("test.xlsx");

            // Assert
            Assert.Equal(1, result.ElementsMidas[0].NodeIds.Length);
        }

        // ==================== Вспомогательные методы ====================

        private Mock<ExcelPackageWrapper> CreateMockPackageData()
        {
            var packageMock = new Mock<ExcelPackageWrapper>();

            // Настраиваем mock для Worksheet с узлами MIDAS
            var sheet1Mock = new Mock<IXLWorksheet>();
            sheet1Mock.Setup(s => s.Dimension).Returns(new XLWorkbookDimensions() { Rows = 3 });
            sheet1Mock.Setup(s => s.Cells[2, 1]).Returns(CreateMockCell("1"));
            sheet1Mock.Setup(s => s.Cells[2, 2]).Returns(CreateMockCell("0"));
            sheet1Mock.Setup(s => s.Cells[2, 3]).Returns(CreateMockCell("1"));
            sheet1Mock.Setup(s => s.Cells[2, 4]).Returns(CreateMockCell("0"));
            sheet1Mock.Setup(s => s.Cells[2, 5]).Returns(CreateMockCell("0.02"));
            sheet1Mock.Setup(s => s.Cells[3, 1]).Returns(CreateMockCell("2"));
            sheet1Mock.Setup(s => s.Cells[3, 2]).Returns(CreateMockCell("1"));
            sheet1Mock.Setup(s => s.Cells[3, 3]).Returns(CreateMockCell("2"));
            sheet1Mock.Setup(s => s.Cells[3, 4]).Returns(CreateMockCell("0"));
            sheet1Mock.Setup(s => s.Cells[3, 5]).Returns(CreateMockCell("0.03"));

            // Настраиваем mock для Worksheet с узлами ЛИРА
            var sheet2Mock = new Mock<IXLWorksheet>();
            sheet2Mock.Setup(s => s.Dimension).Returns(new XLWorkbookDimensions() { Rows = 3 });
            sheet2Mock.Setup(s => s.Cells[2, 1]).Returns(CreateMockCell("10"));
            sheet2Mock.Setup(s => s.Cells[2, 2]).Returns(CreateMockCell("0"));
            sheet2Mock.Setup(s => s.Cells[2, 3]).Returns(CreateMockCell("1"));
            sheet2Mock.Setup(s => s.Cells[2, 4]).Returns(CreateMockCell("0"));
            sheet2Mock.Setup(s => s.Cells[3, 1]).Returns(CreateMockCell("20"));
            sheet2Mock.Setup(s => s.Cells[3, 2]).Returns(CreateMockCell("1"));
            sheet2Mock.Setup(s => s.Cells[3, 3]).Returns(CreateMockCell("2"));
            sheet2Mock.Setup(s => s.Cells[3, 4]).Returns(CreateMockCell("0"));

            // Настраиваем mock для Worksheet с элементами MIDAS
            var sheet3Mock = new Mock<IXLWorksheet>();
            sheet3Mock.Setup(s => s.Dimension).Returns(new XLWorkbookDimensions() { Rows = 3 });
            sheet3Mock.Setup(s => s.Cells[2, 1]).Returns(CreateMockCell("1"));
            sheet3Mock.Setup(s => s.Cells[2, 2]).Returns(CreateMockCell("1"));
            sheet3Mock.Setup(s => s.Cells[2, 3]).Returns(CreateMockCell("2"));

            // Настраиваем mock для Worksheet с элементами ЛИРА
            var sheet4Mock = new Mock<IXLWorksheet>();
            sheet4Mock.Setup(s => s.Dimension).Returns(new XLWorkbookDimensions() { Rows = 3 });
            sheet4Mock.Setup(s => s.Cells[2, 1]).Returns(CreateMockCell("100"));
            sheet4Mock.Setup(s => s.Cells[2, 2]).Returns(CreateMockCell("1"));
            sheet4Mock.Setup(s => s.Cells[2, 3]).Returns(CreateMockCell("2"));

            // Настраиваем mock для Worksheet с напряжениями
            var sheet5Mock = new Mock<IXLWorksheet>();
            sheet5Mock.Setup(s => s.Dimension).Returns(new XLWorkbookDimensions() { Rows = 3 });
            sheet5Mock.Setup(s => s.Cells[2, 1]).Returns(CreateMockCell("1"));
            sheet5Mock.Setup(s => s.Cells[2, 2]).Returns(CreateMockCell("1"));
            sheet5Mock.Setup(s => s.Cells[2, 3]).Returns(CreateMockCell("2"));
            sheet5Mock.Setup(s => s.Cells[2, 4]).Returns(CreateMockCell("0"));
            sheet5Mock.Setup(s => s.Cells[2, 5]).Returns(CreateMockCell("0"));
            sheet5Mock.Setup(s => s.Cells[2, 6]).Returns(CreateMockCell("0"));
            sheet5Mock.Setup(s => s.Cells[2, 7]).Returns(CreateMockCell("0"));
            sheet5Mock.Setup(s => s.Cells[2, 8]).Returns(CreateMockCell("0"));
            sheet5Mock.Setup(s => s.Cells[2, 9]).Returns(CreateMockCell("0"));
            sheet5Mock.Setup(s => s.Cells[2, 10]).Returns(CreateMockCell("100.5"));

            var worksheetsMock = new Mock<IXLWorksheets>();
            worksheetsMock.Setup(w => w["Sheet1"]).Returns(sheet1Mock.Object);
            worksheetsMock.Setup(w => w["Sheet2"]).Returns(sheet2Mock.Object);
            worksheetsMock.Setup(w => w["Sheet3"]).Returns(sheet3Mock.Object);
            worksheetsMock.Setup(w => w["Sheet4"]).Returns(sheet4Mock.Object);
            worksheetsMock.Setup(w => w["Sheet5"]).Returns(sheet5Mock.Object);

            var workbookMock = new Mock<IXLWorkbook>();
            workbookMock.Setup(w => w.Worksheets).Returns(worksheetsMock.Object);

            packageMock.Setup(p => p.Workbook).Returns(workbookMock.Object);

            // Заменяем вызов new ExcelPackage на наш mock
            // Это требует адаптации кода ExcelReader или создания интерфейса
            // Для простоты тестов мы создадим временный файл
            CreateTempExcelFile("test.xlsx");

            return packageMock;
        }

        private IXLCell CreateMockCell(string value)
        {
            var cellMock = new Mock<IXLCell>();
            if (int.TryParse(value, out int intValue))
            {
                cellMock.Setup(c => c.GetValue<int>()).Returns(intValue);
            }
            if (double.TryParse(value, out double doubleValue))
            {
                cellMock.Setup(c => c.GetValue<double>()).Returns(doubleValue);
            }
            return cellMock.Object;
        }

        private void CreateTempExcelFile(string path)
        {
            // Создаем временный файл с базовой структурой для тестов
            using (var package = new ExcelPackage())
            {
                var workbook = package.Workbook;
                var worksheet1 = workbook.AddWorksheet("Sheet1");
                worksheet1.Cell("A1").Value = "Id";
                worksheet1.Cell("B1").Value = "X";
                worksheet1.Cell("C1").Value = "Y";
                worksheet1.Cell("D1").Value = "Z";
                worksheet1.Cell("E1").Value = "NodeDisplacement";
                worksheet1.Cell("A2").Value = 1;
                worksheet1.Cell("B2").Value = 0;
                worksheet1.Cell("C2").Value = 1;
                worksheet1.Cell("D2").Value = 0;
                worksheet1.Cell("E2").Value = 0.02;
                worksheet1.Cell("A3").Value = 2;
                worksheet1.Cell("B3").Value = 1;
                worksheet1.Cell("C3").Value = 2;
                worksheet1.Cell("D3").Value = 0;
                worksheet1.Cell("E3").Value = 0.03;

                var worksheet2 = workbook.AddWorksheet("Sheet2");
                worksheet2.Cell("A1").Value = "Id";
                worksheet2.Cell("B1").Value = "X";
                worksheet2.Cell("C1").Value = "Y";
                worksheet2.Cell("D1").Value = "Z";
                worksheet2.Cell("A2").Value = 10;
                worksheet2.Cell("B2").Value = 0;
                worksheet2.Cell("C2").Value = 1;
                worksheet2.Cell("D2").Value = 0;
                worksheet2.Cell("A3").Value = 20;
                worksheet2.Cell("B3").Value = 1;
                worksheet2.Cell("C3").Value = 2;
                worksheet2.Cell("D3").Value = 0;

                var worksheet3 = workbook.AddWorksheet("Sheet3");
                worksheet3.Cell("A1").Value = "Id";
                worksheet3.Cell("B1").Value = "Node1";
                worksheet3.Cell("C1").Value = "Node2";
                worksheet3.Cell("D1").Value = "Node3";
                worksheet3.Cell("E1").Value = "Node4";
                worksheet3.Cell("A2").Value = 1;
                worksheet3.Cell("B2").Value = 1;
                worksheet3.Cell("C2").Value = 2;
                worksheet3.Cell("D2").Value = 0;
                worksheet3.Cell("E2").Value = 0;

                var worksheet4 = workbook.AddWorksheet("Sheet4");
                worksheet4.Cell("A1").Value = "Id";
                worksheet4.Cell("B1").Value = "Node1";
                worksheet4.Cell("C1").Value = "Node2";
                worksheet4.Cell("D1").Value = "Node3";
                worksheet4.Cell("E1").Value = "Node4";
                worksheet4.Cell("A2").Value = 100;
                worksheet4.Cell("B2").Value = 1;
                worksheet4.Cell("C2").Value = 2;
                worksheet4.Cell("D2").Value = 0;
                worksheet4.Cell("E2").Value = 0;

                var worksheet5 = workbook.AddWorksheet("Sheet5");
                worksheet5.Cell("A1").Value = "Id";
                worksheet5.Cell("B1").Value = "Node1";
                worksheet5.Cell("C1").Value = "Node2";
                worksheet5.Cell("D1").Value = "Node3";
                worksheet5.Cell("E1").Value = "Node4";
                worksheet5.Cell("F1").Value = "Node5";
                worksheet5.Cell("G1").Value = "Node6";
                worksheet5.Cell("H1").Value = "Node7";
                worksheet5.Cell("I1").Value = "Node8";
                worksheet5.Cell("J1").Value = "StressValue";
                worksheet5.Cell("A2").Value = 1;
                worksheet5.Cell("B2").Value = 1;
                worksheet5.Cell("C2").Value = 2;
                worksheet5.Cell("D2").Value = 0;
                worksheet5.Cell("E2").Value = 0;
                worksheet5.Cell("F2").Value = 0;
                worksheet5.Cell("G2").Value = 0;
                worksheet5.Cell("H2").Value = 0;
                worksheet5.Cell("I2").Value = 0;
                worksheet5.Cell("J2").Value = 100.5;

                package.SaveAs(path);
            }
        }
    }

    // Вспомогательные интерфейсы для мокирования
    public interface IXLWorkbook
    {
        IXLWorksheets Worksheets { get; }
    }

    public interface IXLWorksheets
    {
        IXLWorksheet this[string name] { get; }
    }

    public interface IXLWorksheet
    {
        XLWorkbookDimensions Dimension { get; }
        IXLCell this[int row, int col] { get; }
    }

    public interface IXLCell
    {
        T GetValue<T>();
    }

    public struct XLWorkbookDimensions
    {
        public int Rows { get; set; }
    }

    public interface IXLRange
    {
        IXLCell this[int row, int col] { get; }
    }
}
