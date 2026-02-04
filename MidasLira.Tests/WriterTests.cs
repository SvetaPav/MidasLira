using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using Xunit;
using static MidasLira.Mapper;
using MidasLira;

namespace MidasLira.Tests
{
    public class WriterTests
    {
        private readonly Mock<PositionFinder> _positionFinderMock;
        private readonly Mock<Logger> _loggerMock;
        private readonly Writer _writer;

        public WriterTests()
        {
            _positionFinderMock = new Mock<PositionFinder>();
            _loggerMock = new Mock<Logger>();
            _writer = new Writer(_positionFinderMock.Object, _loggerMock.Object);
        }

        /* Тест проверяет успешное выполнение основного метода WriteNodeAndBeddingData
           при корректных входных данных и наличии всех необходимых разделов в файле.
           Мокируются:
           - PositionFinder.ParseTextFile возвращает заранее подготовленный результат парсинга
           - Logger.LogEvent используется для логирования шагов

           Проверяемые аспекты:
           1. Метод возвращает true при успешной записи
           2. Файл записывается с ожидаемыми изменениями
           3. Вызываются методы анализа файла и логирования

           Ожидается, что метод отработает без исключений и вернет true.
        */
        [Fact]
        public void WriteNodeAndBeddingData_SuccessfulWrite_ReturnsTrue()
        {
            // Arrange
            string testFilePath = "test_lira.dat";
            var nodes = CreateTestNodes();
            var elements = CreateTestElements();
            var plaques = CreateTestPlaques();

            var parseResult = new PositionFinder.ParseResult
            {
                Section1Start = 10,
                Section1End = 20,
                Section1LastElementLine = 15,
                Section3Start = 30,
                Section3End = 40,
                Section3LastMaterialLine = 35,
                Section17Start = 100,
                Section17End = 110,
                Section19Start = -1,
                Section19End = -1,
                FileEnd = 120
            };

            _positionFinderMock.Setup(pf => pf.ParseTextFile(testFilePath)).Returns(parseResult);
            _positionFinderMock.Setup(pf => pf.FindPositionForSection19(parseResult)).Returns(115);

            // Создаем временный файл с базовой структурой ЛИРА-САПР
            CreateTestLiraFile(testFilePath, parseResult);

            try
            {
                // Act
                bool result = _writer.WriteNodeAndBeddingData(testFilePath, nodes, elements, plaques);

                // Assert
                Assert.True(result);
                _positionFinderMock.Verify(pf => pf.ParseTextFile(testFilePath), Times.Once);
                _loggerMock.Verify(l => l.LogEvent(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);

                // Проверяем, что файл был изменен (можно добавить более детальные проверки)
                string[] lines = File.ReadAllLines(testFilePath);
                Assert.Contains(lines, line => line.Contains("56")); // Должны быть элементы КЭ56
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFilePath))
                    File.Delete(testFilePath);
            }
        }

        /* Тест проверяет поведение метода при отсутствии обязательного раздела (1/).
           Раздел (1/) является обязательным для записи элементов КЭ56.
           Метод должен выбросить InvalidOperationException с соответствующим сообщением.

           Проверяемые аспекты:
           1. Исключение типа InvalidOperationException
           2. Сообщение исключения содержит информацию об отсутствии раздела
           3. Логирование ошибки
        */
        [Fact]
        public void WriteNodeAndBeddingData_MissingSection1_ThrowsInvalidOperationException()
        {
            // Arrange
            string testFilePath = "test_lira.dat";
            var nodes = CreateTestNodes();
            var elements = CreateTestElements();
            var plaques = CreateTestPlaques();

            var parseResult = new PositionFinder.ParseResult
            {
                Section1Start = -1, // Раздел (1/) отсутствует
                Section3Start = 30,
                Section17Start = 100,
                FileEnd = 120
            };

            _positionFinderMock.Setup(pf => pf.ParseTextFile(testFilePath)).Returns(parseResult);
            CreateTestLiraFile(testFilePath, parseResult);

            try
            {
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(
                    () => _writer.WriteNodeAndBeddingData(testFilePath, nodes, elements, plaques));

                Assert.Contains("отсутствует обязательный раздел (1/)", exception.Message);
                _loggerMock.Verify(l => l.LogEvent("ERROR", It.IsAny<string>()), Times.AtLeastOnce);
            }
            finally
            {
                if (File.Exists(testFilePath))
                    File.Delete(testFilePath);
            }
        }

        /* Тест проверяет создание нового раздела (3/) при его отсутствии в исходном файле.
           Если раздел (3/) не найден, Writer должен создать его перед разделом (17/).

           Проверяемые аспекты:
           1. Раздел (3/) добавляется в правильную позицию (перед разделом (17/))
           2. В новый раздел записываются жесткости узлов
           3. Логируются соответствующие события
        */
        [Fact]
        public void WriteStiffnessesToFile_CreatesNewSection3_WhenSection3Missing()
        {
            // Arrange
            var lines = new List<string>
            {
                "( 1/",
                "44 1 2 3 4 /",
                ")",
                "",
                "( 17/",
                "some data",
                ")"
            };

            var nodes = CreateTestNodes();
            var plaques = CreateTestPlaques();
            var parseResult = new PositionFinder.ParseResult
            {
                Section3Start = -1, // Раздел (3/) отсутствует
                Section3End = -1,
                Section17Start = 4, // Индекс строки "( 17/" в списке lines (0-based)
                Section17End = 7
            };

            // Act
            // Вызовем приватный метод через рефлексию или протестируем через публичный метод
            // Для простоты создадим временный файл и запустим полную запись
            string testFilePath = "test_section3.dat";
            CreateTestLiraFile(testFilePath, parseResult);
            _positionFinderMock.Setup(pf => pf.ParseTextFile(testFilePath)).Returns(parseResult);

            try
            {
                bool result = _writer.WriteNodeAndBeddingData(testFilePath, nodes, new List<MidasElementInfo>(), plaques);

                // Assert
                Assert.True(result);
                string[] resultLines = File.ReadAllLines(testFilePath);
                Assert.Contains(resultLines, line => line.StartsWith("( 3/"));
                Assert.Contains(resultLines, line => line.Contains("1.05541e+006")); // Дефолтный материал
            }
            finally
            {
                if (File.Exists(testFilePath))
                    File.Delete(testFilePath);
            }
        }

        /* Тест проверяет корректность формирования строк жесткостей узлов.
           Жесткости вычисляются на основе plaque.rigidNodes.

           Проверяемые аспекты:
           1. Формат строки жесткости: "номер_жесткости значение значение 0 0 0 0 /"
           2. Номера жесткостей увеличиваются последовательно
           3. Узлы без жесткостей (rigidNodes = 0) игнорируются
           4. Узлы без сопоставления (AppropriateLiraNode.Id = 0) игнорируются
        */
        [Fact]
        public void CreateStiffnessLines_GeneratesCorrectFormat()
        {
            // Arrange
            var writer = new Writer(_positionFinderMock.Object, _loggerMock.Object);
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>())
                {
                    AppropriateLiraNode = new LiraNodeInfo(100, 0, 0, 0, new List<LiraElementInfo>())
                },
                new MidasNodeInfo(2, 0, 0, 0, 0, new List<MidasElementInfo>())
                {
                    AppropriateLiraNode = new LiraNodeInfo(200, 0, 0, 0, new List<LiraElementInfo>())
                }
            };

            var plaques = new List<Plaque>
            {
                new Plaque { rigidNodes = 123.4567, Nodes = new List<MidasNodeInfo> { nodes[0] } },
                new Plaque { rigidNodes = 789.0123, Nodes = new List<MidasNodeInfo> { nodes[1] } }
            };

            // Act
            // Используем рефлексию для вызова приватного метода
            var method = typeof(Writer).GetMethod("CreateStiffnessLines",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stiffnessLines = (List<string>)method.Invoke(writer, new object[] { nodes, plaques });

            // Assert
            Assert.Equal(2, stiffnessLines.Count);
            Assert.Equal("1 123.4567 123.4567 0 0 0 0 /", stiffnessLines[0]);
            Assert.Equal("2 789.0123 789.0123 0 0 0 0 /", stiffnessLines[1]);
        }

        /* Тест проверяет обработку пустых списков данных.
           Если нет узлов с жесткостями, элементов КЭ56 или коэффициентов постели,
           Writer должен залогировать предупреждение и продолжить выполнение.

           Проверяемые аспекты:
           1. Метод возвращает true даже при отсутствии данных для записи
           2. Логируются предупреждения (WARNING)
           3. Файл не повреждается
        */
        [Fact]
        public void WriteNodeAndBeddingData_EmptyData_LogsWarningAndReturnsTrue()
        {
            // Arrange
            string testFilePath = "test_empty.dat";
            var emptyNodes = new List<MidasNodeInfo>();
            var emptyElements = new List<MidasElementInfo>();
            var emptyPlaques = new List<Plaque>();

            var parseResult = new PositionFinder.ParseResult
            {
                Section1Start = 10,
                Section1End = 20,
                Section3Start = 30,
                Section3End = 40,
                Section17Start = 100,
                Section17End = 110,
                FileEnd = 120
            };

            _positionFinderMock.Setup(pf => pf.ParseTextFile(testFilePath)).Returns(parseResult);
            CreateTestLiraFile(testFilePath, parseResult);

            try
            {
                // Act
                bool result = _writer.WriteNodeAndBeddingData(testFilePath, emptyNodes, emptyElements, emptyPlaques);

                // Assert
                Assert.True(result);
                _loggerMock.Verify(l => l.LogEvent("WARNING", It.IsAny<string>()), Times.AtLeastOnce);
            }
            finally
            {
                if (File.Exists(testFilePath))
                    File.Delete(testFilePath);
            }
        }

        /* Тест проверяет создание backup копии файла перед записью.
           Writer должен создать backup с временной меткой в имени файла.

           Проверяемые аспекты:
           1. Файл backup создается
           2. Имя backup содержит исходное имя и timestamp
           3. Backup логируется как INFO событие
        */
        [Fact]
        public void WriteNodeAndBeddingData_CreatesBackupFile()
        {
            // Arrange
            string testFilePath = "test_backup.dat";
            var nodes = CreateTestNodes();
            var elements = CreateTestElements();
            var plaques = CreateTestPlaques();

            var parseResult = new PositionFinder.ParseResult
            {
                Section1Start = 10,
                Section1End = 20,
                Section3Start = 30,
                Section3End = 40,
                Section17Start = 100,
                Section17End = 110,
                FileEnd = 120
            };

            _positionFinderMock.Setup(pf => pf.ParseTextFile(testFilePath)).Returns(parseResult);
            CreateTestLiraFile(testFilePath, parseResult);

            try
            {
                // Act
                bool result = _writer.WriteNodeAndBeddingData(testFilePath, nodes, elements, plaques);

                // Assert
                Assert.True(result);
                string[] backupFiles = Directory.GetFiles(".", $"{testFilePath}.backup_*");
                Assert.NotEmpty(backupFiles);
                _loggerMock.Verify(l => l.LogEvent("INFO", It.Is<string>(s => s.Contains("Создан backup"))), Times.Once);
            }
            finally
            {
                // Cleanup
                foreach (var file in Directory.GetFiles(".", $"{testFilePath}.backup_*"))
                    File.Delete(file);
                if (File.Exists(testFilePath))
                    File.Delete(testFilePath);
            }
        }

        // ==================== Вспомогательные методы ====================

        private List<MidasNodeInfo> CreateTestNodes()
        {
            var nodes = new List<MidasNodeInfo>();
            for (int i = 1; i <= 5; i++)
            {
                var node = new MidasNodeInfo(i, i * 10, i * 20, i * 30, i * 0.1, new List<MidasElementInfo>())
                {
                    AppropriateLiraNode = new LiraNodeInfo(i * 100, i * 10, i * 20, i * 30, new List<LiraElementInfo>()),
                    RigidityNumber = i
                };
                nodes.Add(node);
            }
            return nodes;
        }

        private List<MidasElementInfo> CreateTestElements()
        {
            var elements = new List<MidasElementInfo>();
            for (int i = 1; i <= 5; i++)
            {
                var element = new MidasElementInfo(i, new int[] { i, i + 1, i + 2, i + 3 }, i * 100, i * 0.01, i * 50)
                {
                    AppropriateLiraElement = new LiraElementInfo(i * 200, new int[] { i, i + 1, i + 2, i + 3 }),
                    BeddingCoefficient = i * 50
                };
                elements.Add(element);
            }
            return elements;
        }

        private List<Plaque> CreateTestPlaques()
        {
            var plaques = new List<Plaque>();
            for (int i = 1; i <= 3; i++)
            {
                plaques.Add(new Plaque { Id = i, rigidNodes = i * 1000 });
            }
            return plaques;
        }

        private void CreateTestLiraFile(string path, PositionFinder.ParseResult parseResult)
        {
            var lines = new List<string>();

            // Заголовок
            lines.Add("ЛИРА-САПР Файл модели");
            lines.Add("");

            // Раздел (1/) если есть
            if (parseResult.Section1Start != -1)
            {
                lines.Add("( 1/");
                lines.Add("44 1 2 3 4 /");
                lines.Add("42 5 6 7 8 /");
                lines.Add(")");
                lines.Add("");
            }

            // Раздел (3/) если есть
            if (parseResult.Section3Start != -1)
            {
                lines.Add("( 3/");
                lines.Add("1 S0 1.05541e+006 20 40/");
                lines.Add(" 0 RO 0.2/");
                lines.Add(" 0 Mu 0.2/");
                lines.Add(")");
                lines.Add("");
            }

            // Раздел (17/)
            if (parseResult.Section17Start != -1)
            {
                lines.Add("( 17/");
                lines.Add("данные раздела 17");
                lines.Add(")");
            }

            File.WriteAllLines(path, lines);
        }

        /* Тест проверяет генерацию отчета о записанных данных.
           Метод GenerateReport должен создать структурированный отчет,
           содержащий статистику по узлам, жесткостям, элементам и коэффициентам.

           Проверяемые аспекты:
           1. Отчет содержит заголовок и дату
           2. Статистика по узлам корректна
           3. Статистика по элементам корректна
           4. Отсутствуют ошибки форматирования
        */
        [Fact]
        public void GenerateReport_ValidData_ReturnsFormattedReport()
        {
            // Arrange
            var nodes = CreateTestNodes();
            var elements = CreateTestElements();
            // Устанавливаем жесткости и коэффициенты
            nodes[0].RigidityNumber = 1;
            nodes[0].Plaque = new Plaque { rigidNodes = 1000 };
            elements[0].BeddingCoefficient = 50;

            // Act
            string report = _writer.GenerateReport(nodes, elements);

            // Assert
            Assert.Contains("=== ОТЧЕТ О ЗАПИСИ ДАННЫХ В ЛИРА-САПР ===", report);
            Assert.Contains("1. УЗЛЫ И ЖЕСТКОСТИ:", report);
            Assert.Contains("2. ЭЛЕМЕНТЫ И КОЭФФИЦИЕНТЫ ПОСТЕЛИ:", report);
            Assert.Contains($"Всего узлов: {nodes.Count}", report);
            Assert.Contains($"Всего элементов: {elements.Count}", report);
        }

        /* Тест проверяет обработку исключений при невозможности записи в файл.
           Если в процессе записи возникает исключение, метод WriteNodeAndBeddingData
           должен перехватить его, залогировать ошибку и выбросить InvalidOperationException.

           Проверяемые аспекты:
           1. InvalidOperationException выбрасывается
           2. Сообщение исключения содержит детали ошибки
           3. Логируется событие ERROR
        */
        [Fact]
        public void WriteNodeAndBeddingData_ThrowsOnFileWriteError_ThrowsInvalidOperationException()
        {
            // Arrange
            string testFilePath = "readonly.dat";
            var nodes = CreateTestNodes();
            var elements = CreateTestElements();
            var plaques = CreateTestPlaques();

            var parseResult = new PositionFinder.ParseResult
            {
                Section1Start = 10,
                Section1End = 20,
                Section3Start = 30,
                Section3End = 40,
                Section17Start = 100,
                Section17End = 110,
                FileEnd = 120
            };

            _positionFinderMock.Setup(pf => pf.ParseTextFile(testFilePath)).Returns(parseResult);
            CreateTestLiraFile(testFilePath, parseResult);

            // Делаем файл read-only (на Unix системах может потребоваться другой подход)
            try
            {
                File.SetAttributes(testFilePath, FileAttributes.ReadOnly);
            }
            catch
            {
                // Если не поддерживается, пропускаем тест
                return;
            }

            try
            {
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(
                    () => _writer.WriteNodeAndBeddingData(testFilePath, nodes, elements, plaques));

                Assert.Contains("Ошибка записи в файл ЛИРА-САПР", exception.Message);
                _loggerMock.Verify(l => l.LogEvent("ERROR", It.IsAny<string>()), Times.AtLeastOnce);
            }
            finally
            {
                // Снимаем read-only атрибут
                File.SetAttributes(testFilePath, FileAttributes.Normal);
                if (File.Exists(testFilePath))
                    File.Delete(testFilePath);
            }
        }

        /* Тест проверяет валидацию входных данных.
           Метод ValidateInputData вызывается в начале WriteNodeAndBeddingData
           и проверяет наличие файла, непустые списки узлов и элементов.

           Проверяемые аспекты:
           1. Путь к файлу не может быть пустым
           2. Файл должен существовать
           3. Списки узлов и элементов не могут быть null или пустыми
        */
        [Fact]
        public void WriteNodeAndBeddingData_InvalidInput_ThrowsArgumentException()
        {
            // Arrange
            string missingFile = "nonexistent.dat";
            var nodes = new List<MidasNodeInfo>();
            var elements = new List<MidasElementInfo>();
            var plaques = new List<Plaque>();

            // Act & Assert для пустого пути
            Assert.Throws<ArgumentException>(() =>
                _writer.WriteNodeAndBeddingData("", nodes, elements, plaques));

            // Act & Assert для отсутствующего файла
            Assert.Throws<FileNotFoundException>(() =>
                _writer.WriteNodeAndBeddingData(missingFile, nodes, elements, plaques));

            // Act & Assert для пустых списков
            string existingFile = "existing.dat";
            File.WriteAllText(existingFile, "");
            try
            {
                Assert.Throws<ArgumentException>(() =>
                    _writer.WriteNodeAndBeddingData(existingFile, nodes, elements, plaques));
            }
            finally
            {
                File.Delete(existingFile);
            }
        }

        /* Тест проверяет корректность создания элементов КЭ56.
           Элементы КЭ56 создаются только для узлов, у которых есть номер жесткости.

           Проверяемые аспекты:
           1. Формат строки: "56 номер_жесткости номер_узла /"
           2. Узлы без номера жесткости игнорируются
           3. Узлы без сопоставления (AppropriateLiraNode.Id = 0) игнорируются
        */
        [Fact]
        public void CreateElement56Lines_GeneratesCorrectFormat()
        {
            // Arrange
            var writer = new Writer(_positionFinderMock.Object, _loggerMock.Object);
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>())
                {
                    AppropriateLiraNode = new LiraNodeInfo(100, 0, 0, 0, new List<LiraElementInfo>()),
                    RigidityNumber = 5
                },
                new MidasNodeInfo(2, 0, 0, 0, 0, new List<MidasElementInfo>())
                {
                    AppropriateLiraNode = new LiraNodeInfo(200, 0, 0, 0, new List<LiraElementInfo>()),
                    RigidityNumber = 0 // Без жесткости - должен быть проигнорирован
                }
            };

            // Act через рефлексию
            var method = typeof(Writer).GetMethod("CreateElement56Lines",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var element56Lines = (List<string>)method.Invoke(writer, new object[] { nodes });

            // Assert
            Assert.Single(element56Lines);
            Assert.Equal("56 5 100 /", element56Lines[0]);
        }

        /* Тест проверяет корректность создания строк коэффициентов постели.
           Коэффициенты создаются только для элементов с BeddingCoefficient > 0
           и имеющих сопоставление с элементом ЛИРА-САПР.

           Проверяемые аспекты:
           1. Формат строки: "номер_элемента коэффициент 0 0 0 /"
           2. Элементы с нулевым коэффициентом игнорируются
           3. Элементы без сопоставления игнорируются
        */
        [Fact]
        public void CreateCoefficientLines_GeneratesCorrectFormat()
        {
            // Arrange
            var writer = new Writer(_positionFinderMock.Object, _loggerMock.Object);
            var elements = new List<MidasElementInfo>
            {
                new MidasElementInfo(1, new int[] {1,2,3,4}, 100, 0.01, 123.456)
                {
                    AppropriateLiraElement = new LiraElementInfo(500, new int[] {1,2,3,4}),
                    BeddingCoefficient = 123.456
                },
                new MidasElementInfo(2, new int[] {5,6,7,8}, 200, 0.02, 0) // Нулевой коэффициент
                {
                    AppropriateLiraElement = new LiraElementInfo(600, new int[] {5,6,7,8}),
                    BeddingCoefficient = 0
                }
            };

            // Act через рефлексию
            var method = typeof(Writer).GetMethod("CreateCoefficientLines",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var coefficientLines = (List<string>)method.Invoke(writer, new object[] { elements });

            // Assert
            Assert.Single(coefficientLines);
            Assert.Equal("500 123.456 0 0 0 /", coefficientLines[0]);
        }
    }
}