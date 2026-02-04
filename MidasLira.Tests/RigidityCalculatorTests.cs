using System;
using System.Collections.Generic;
using System.Linq;
using MidasLira;
using Moq;
using Xunit;
using static MidasLira.Mapper;

namespace MidasLira.Tests
{
    public class RigidityCalculatorTests
    {
        private readonly RigidityCalculator _calculator;

        public RigidityCalculatorTests()
        {
            _calculator = new RigidityCalculator();
        }

        /* Тест проверяет успешное вычисление жесткостей узлов для простого случая
           с одной плитой, содержащей один четырехугольный элемент.
           Проверяемые аспекты:
           1. Метод возвращает непустой список плит
           2. Плита содержит вычисленное значение rigidNodes
           3. Значение rigidNodes вычислено по формуле: (area * avgC1 * 0.7) / количество элементов
           4. Все узлы и элементы корректно сгруппированы в плиту
        */
        [Fact]
        public void CalculateNodeRigidities_SingleQuadElement_ReturnsCorrectRigidity()
        {
            // Arrange
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(2, 1, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(3, 1, 1, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(4, 0, 1, 0, 0, new List<MidasElementInfo>())
            };

            var element = new MidasElementInfo(1, new int[] {1, 2, 3, 4}, 100, 0.01, 50.0);
            var elements = new List<MidasElementInfo> { element };

            // Act
            var plaques = _calculator.CalculateNodeRigidities(nodes, elements);

            // Assert
            Assert.Single(plaques);
            var plaque = plaques[0];
            Assert.Single(plaque.Elements);
            Assert.Equal(4, plaque.Nodes.Count);

            // Проверяем, что жесткость вычислена (площадь квадрата 1x1 = 1, средний C1 = 50)
            // Ожидаемое значение: (1 * 50 * 0.7) / 1 = 35
            double expectedRigidity = (1.0 * 50.0 * 0.7) / 1;
            Assert.Equal(expectedRigidity, plaque.rigidNodes, 5); // допуск 5 знаков после запятой
        }

        /* Тест проверяет обработку пустых входных данных.
           Метод должен выбрасывать ArgumentNullException для null списков
           и ArgumentException для пустых списков.

           Проверяемые аспекты:
           1. ArgumentNullException для nodes = null
           2. ArgumentNullException для elements = null
           3. ArgumentException для пустого списка nodes
           4. ArgumentException для пустого списка elements
        */
        [Fact]
        public void CalculateNodeRigidities_NullOrEmptyInput_ThrowsArgumentException()
        {
            // Arrange
            var validNodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>())
            };
            var validElements = new List<MidasElementInfo>
            {
                new MidasElementInfo(1, new int[] {1}, 100, 0.01, 50.0)
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _calculator.CalculateNodeRigidities(null, validElements));
            Assert.Throws<ArgumentNullException>(() =>
                _calculator.CalculateNodeRigidities(validNodes, null));
            Assert.Throws<ArgumentException>(() =>
                _calculator.CalculateNodeRigidities(new List<MidasNodeInfo>(), validElements));
            Assert.Throws<ArgumentException>(() =>
                _calculator.CalculateNodeRigidities(validNodes, new List<MidasElementInfo>()));
        }

        /* Тест проверяет вычисление жесткости для плиты с несколькими элементами.
           Плита содержит два треугольных элемента, образующих квадрат.

           Проверяемые аспекты:
           1. Все элементы сгруппированы в одну плиту (т.к. они имеют общие узлы)
           2. Жесткость вычислена на основе общей площади и среднего коэффициента
           3. Количество узлов в плите корректно
        */
        [Fact]
        public void CalculateNodeRigidities_MultipleElementsInOnePlaque_ReturnsCorrectRigidity()
        {
            // Arrange
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(2, 1, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(3, 1, 1, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(4, 0, 1, 0, 0, new List<MidasElementInfo>())
            };

            // Два треугольника, образующих квадрат
            var element1 = new MidasElementInfo(1, new int[] {1, 2, 3}, 100, 0.01, 30.0);
            var element2 = new MidasElementInfo(2, new int[] {1, 3, 4}, 200, 0.02, 40.0);
            var elements = new List<MidasElementInfo> { element1, element2 };

            // Act
            var plaques = _calculator.CalculateNodeRigidities(nodes, elements);

            // Assert
            Assert.Single(plaques); // Оба элемента должны быть в одной плите
            var plaque = plaques[0];
            Assert.Equal(2, plaque.Elements.Count);
            Assert.Equal(4, plaque.Nodes.Count);

            // Площадь каждого треугольника = 0.5, общая площадь = 1.0
            // Средний C1 = (30 + 40) / 2 = 35
            // Ожидаемая жесткость: (1.0 * 35 * 0.7) / 2 = 12.25
            double expectedRigidity = (1.0 * 35.0 * 0.7) / 2;
            Assert.Equal(expectedRigidity, plaque.rigidNodes, 5);
        }

        /* Тест проверяет разделение элементов на несколько плит.
           Элементы, не имеющие общих узлов, должны быть сгруппированы в разные плиты.

           Проверяемые аспекты:
           1. Создаются две отдельные плиты
           2. Каждая плита содержит свои элементы и узлы
           3. Жесткость вычисляется независимо для каждой плиты
        */
        [Fact]
        public void CalculateNodeRigidities_MultiplePlaques_ReturnsSeparatedPlaques()
        {
            // Arrange
            var nodes = new List<MidasNodeInfo>
            {
                // Первая плита (квадрат 1x1)
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(2, 1, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(3, 1, 1, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(4, 0, 1, 0, 0, new List<MidasElementInfo>()),
                // Вторая плита (квадрат 2x2, смещенный)
                new MidasNodeInfo(5, 3, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(6, 4, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(7, 4, 1, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(8, 3, 1, 0, 0, new List<MidasElementInfo>())
            };

            var element1 = new MidasElementInfo(1, new int[] {1, 2, 3, 4}, 100, 0.01, 50.0); // Площадь 1
            var element2 = new MidasElementInfo(2, new int[] {5, 6, 7, 8}, 200, 0.02, 100.0); // Площадь 1
            var elements = new List<MidasElementInfo> { element1, element2 };

            // Act
            var plaques = _calculator.CalculateNodeRigidities(nodes, elements);

            // Assert
            Assert.Equal(2, plaques.Count);

            // Проверяем первую плиту
            var plaque1 = plaques[0];
            Assert.Single(plaque1.Elements);
            Assert.Equal(4, plaque1.Nodes.Count);
            double expectedRigidity1 = (1.0 * 50.0 * 0.7) / 1;
            Assert.Equal(expectedRigidity1, plaque1.rigidNodes, 5);

            // Проверяем вторую плиту
            var plaque2 = plaques[1];
            Assert.Single(plaque2.Elements);
            Assert.Equal(4, plaque2.Nodes.Count);
            double expectedRigidity2 = (1.0 * 100.0 * 0.7) / 1;
            Assert.Equal(expectedRigidity2, plaque2.rigidNodes, 5);
        }

        /* Тест проверяет корректность расчета расстояния между двумя узлами.
           Метод Distance статический и используется для вычисления сторон элементов.

           Проверяемые аспекты:
           1. Расстояние между одинаковыми точками равно 0
           2. Расстояние вычисляется по формуле Евклида
           3. Корректность для положительных и отрицательных координат
        */
        [Fact]
        public void Distance_DifferentPoints_ReturnsEuclideanDistance()
        {
            // Arrange
            var point1 = new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>());
            var point2 = new MidasNodeInfo(2, 3, 4, 0, 0, new List<MidasElementInfo>());
            var point3 = new MidasNodeInfo(3, -1, -1, -1, 0, new List<MidasElementInfo>());

            // Act & Assert
            Assert.Equal(0, RigidityCalculator.Distance(point1, point1), 5);
            Assert.Equal(5, RigidityCalculator.Distance(point1, point2), 5); // 3-4-5 треугольник
            Assert.Equal(Math.Sqrt(3), RigidityCalculator.Distance(point1, point3), 5); // √(1²+1²+1²)
        }

        /* Тест проверяет обработку элемента с недостающими узлами.
           Если у элемента есть nodeId, который отсутствует в общем списке узлов,
           метод CalculateElementArea должен выбросить InvalidOperationException.

           Проверяемые аспекты:
           1. Исключение типа InvalidOperationException
           2. Сообщение исключения содержит ID проблемного элемента
        */
        [Fact]
        public void CalculateElementArea_MissingNode_ThrowsInvalidOperationException()
        {
            // Arrange
            var calculator = new RigidityCalculator();
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>())
                // Узел с ID=2 отсутствует
            };
            var element = new MidasElementInfo(1, new int[] {1, 2, 3}, 100, 0.01, 50.0);

            // Act через рефлексию
            var method = typeof(RigidityCalculator).GetMethod("CalculateElementArea",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                method.Invoke(calculator, new object[] { element, nodes }));
            Assert.Contains("не все узлы найдены", exception.Message);
            Assert.Contains("ID=1", exception.Message);
        }

        /* Тест проверяет расчет площади треугольника.
           Метод TriangleArea должен корректно вычислять площадь по трем точкам.

           Проверяемые аспекты:
           1. Площадь прямоугольного треугольника = 0.5 * основание * высота
           2. Площадь равностороннего треугольника по формуле Герона
           3. Площадь вырожденного треугольника (коллинеарные точки) = 0
        */
        [Fact]
        public void TriangleArea_VariousTriangles_ReturnsCorrectArea()
        {
            // Arrange
            var calculator = new RigidityCalculator();

            // Прямоугольный треугольник (катеты 3 и 4)
            var A1 = new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>());
            var B1 = new MidasNodeInfo(2, 3, 0, 0, 0, new List<MidasElementInfo>());
            var C1 = new MidasNodeInfo(3, 0, 4, 0, 0, new List<MidasElementInfo>());

            // Равносторонний треугольник (сторона = 2)
            var A2 = new MidasNodeInfo(4, 0, 0, 0, 0, new List<MidasElementInfo>());
            var B2 = new MidasNodeInfo(5, 2, 0, 0, 0, new List<MidasElementInfo>());
            var C2 = new MidasNodeInfo(6, 1, Math.Sqrt(3), 0, 0, new List<MidasElementInfo>());

            // Вырожденный треугольник (коллинеарные точки)
            var A3 = new MidasNodeInfo(7, 0, 0, 0, 0, new List<MidasElementInfo>());
            var B3 = new MidasNodeInfo(8, 1, 0, 0, 0, new List<MidasElementInfo>());
            var C3 = new MidasNodeInfo(9, 2, 0, 0, 0, new List<MidasElementInfo>());

            // Act через рефлексию
            var method = typeof(RigidityCalculator).GetMethod("TriangleArea",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            double area1 = (double)method.Invoke(calculator, new object[] { A1, B1, C1 });
            double area2 = (double)method.Invoke(calculator, new object[] { A2, B2, C2 });
            double area3 = (double)method.Invoke(calculator, new object[] { A3, B3, C3 });

            // Assert
            Assert.Equal(6.0, area1, 5); // 0.5 * 3 * 4 = 6
            Assert.Equal(Math.Sqrt(3), area2, 5); // Площадь равностороннего: (√3/4)*a² = √3
            Assert.Equal(0, area3, 5); // Коллинеарные точки
        }

        /* Тест проверяет расчет площади четырехугольника.
           Метод QuadrilateralArea должен делить четырехугольник на два треугольника
           и суммировать их площади.

           Проверяемые аспекты:
           1. Площадь квадрата = сторона²
           2. Площадь прямоугольника = ширина * высота
           3. Корректность для невыпуклых четырехугольников
        */
        [Fact]
        public void QuadrilateralArea_VariousQuadrilaterals_ReturnsCorrectArea()
        {
            // Arrange
            var calculator = new RigidityCalculator();

            // Квадрат 2x2
            var A1 = new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>());
            var B1 = new MidasNodeInfo(2, 2, 0, 0, 0, new List<MidasElementInfo>());
            var C1 = new MidasNodeInfo(3, 2, 2, 0, 0, new List<MidasElementInfo>());
            var D1 = new MidasNodeInfo(4, 0, 2, 0, 0, new List<MidasElementInfo>());

            // Прямоугольник 3x4
            var A2 = new MidasNodeInfo(5, 0, 0, 0, 0, new List<MidasElementInfo>());
            var B2 = new MidasNodeInfo(6, 3, 0, 0, 0, new List<MidasElementInfo>());
            var C2 = new MidasNodeInfo(7, 3, 4, 0, 0, new List<MidasElementInfo>());
            var D2 = new MidasNodeInfo(8, 0, 4, 0, 0, new List<MidasElementInfo>());

            // Act через рефлексию
            var method = typeof(RigidityCalculator).GetMethod("QuadrilateralArea",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            double area1 = (double)method.Invoke(calculator, new object[] { A1, B1, C1, D1 });
            double area2 = (double)method.Invoke(calculator, new object[] { A2, B2, C2, D2 });

            // Assert
            Assert.Equal(4.0, area1, 5); // 2*2 = 4
            Assert.Equal(12.0, area2, 5); // 3*4 = 12
        }

        /* Тест проверяет расчет среднего коэффициента постели.
           Метод AverageBeddingCoefficient должен вычислять среднее арифметическое
           коэффициентов постели всех элементов плиты.

           Проверяемые аспекты:
           1. Среднее для одинаковых значений
           2. Среднее для разных значений
           3. Корректность для одного элемента
        */
        [Fact]
        public void AverageBeddingCoefficient_VariousElements_ReturnsCorrectAverage()
        {
            // Arrange
            var calculator = new RigidityCalculator();
            var elements = new List<MidasElementInfo>
            {
                new MidasElementInfo(1, new int[] {1,2,3}, 100, 0.01, 10.0),
                new MidasElementInfo(2, new int[] {4,5,6}, 200, 0.02, 20.0),
                new MidasElementInfo(3, new int[] {7,8,9}, 300, 0.03, 30.0)
            };

            // Act через рефлексию
            var method = typeof(RigidityCalculator).GetMethod("AverageBeddingCoefficient",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            double average = (double)method.Invoke(calculator, new object[] { elements });

            // Assert
            Assert.Equal(20.0, average, 5); // (10+20+30)/3 = 20
        }

        /* Тест проверяет обработку элемента с некорректным количеством узлов.
           Метод CalculateElementArea должен выбрасывать ArgumentException,
           если элемент имеет не 3 и не 4 узла.

           Проверяемые аспекты:
           1. ArgumentException для 2 узлов
           2. ArgumentException для 5 узлов
           3. Сообщение исключения содержит ID элемента
        */
        [Fact]
        public void CalculateElementArea_InvalidNodeCount_ThrowsArgumentException()
        {
            // Arrange
            var calculator = new RigidityCalculator();
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(2, 1, 0, 0, 0, new List<MidasElementInfo>())
            };

            // Элемент с 2 узлами
            var element2Nodes = new MidasElementInfo(1, new int[] {1, 2}, 100, 0.01, 50.0);
            // Элемент с 5 узлов
            var element5Nodes = new MidasElementInfo(2, new int[] {1, 2, 1, 2, 1}, 200, 0.02, 50.0);

            // Act через рефлексию
            var method = typeof(RigidityCalculator).GetMethod("CalculateElementArea",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert
            var exception2 = Assert.Throws<ArgumentException>(() =>
                method.Invoke(calculator, new object[] { element2Nodes, nodes }));
            Assert.Contains("ID=1", exception2.Message);

            var exception5 = Assert.Throws<ArgumentException>(() =>
                method.Invoke(calculator, new object[] { element5Nodes, nodes }));
            Assert.Contains("ID=2", exception5.Message);
        }

        /* Тест проверяет, что жесткость узлов равномерно распределяется
           между элементами плиты (формула делится на количество элементов).

           Проверяемые аспекты:
           1. Жесткость уменьшается с увеличением количества элементов
           2. Общая жесткость плиты пропорциональна площади и среднему коэффициенту
        */
        [Fact]
        public void CalculateNodeRigidities_RigidityScalesWithElementCount()
        {
            // Arrange
            var nodes = new List<MidasNodeInfo>
            {
                new MidasNodeInfo(1, 0, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(2, 1, 0, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(3, 1, 1, 0, 0, new List<MidasElementInfo>()),
                new MidasNodeInfo(4, 0, 1, 0, 0, new List<MidasElementInfo>())
            };

            // Один элемент площадью 1
            var element1 = new MidasElementInfo(1, new int[] {1, 2, 3, 4}, 100, 0.01, 100.0);
            // Два элемента (каждый площадью 0.5, в сумме 1)
            var element2a = new MidasElementInfo(2, new int[] {1, 2, 3}, 100, 0.01, 100.0);
            var element2b = new MidasElementInfo(3, new int[] {1, 3, 4}, 100, 0.01, 100.0);

            // Act
            var plaques1 = _calculator.CalculateNodeRigidities(nodes, new List<MidasElementInfo> { element1 });
            var plaques2 = _calculator.CalculateNodeRigidities(nodes, new List<MidasElementInfo> { element2a, element2b });

            // Assert
            double rigidity1 = plaques1[0].rigidNodes; // (1*100*0.7)/1 = 70
            double rigidity2 = plaques2[0].rigidNodes; // (1*100*0.7)/2 = 35
            Assert.Equal(rigidity1, rigidity2 * 2, 5); // Жесткость должна быть вдвое меньше при вдвое большем количестве элементов
        }
    }
}