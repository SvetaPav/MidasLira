using MidasLira;
using Xunit;

namespace MidasLira.Tests
{
    public class MapperTests
    {
        /* Тест проверяет сопоставление узлов MIDAS с узлами ЛИРА-САПР по координатам.
           Метод MapNodesAndElements ищет узлы ЛИРА с совпадающими координатами
           и присваивает их свойству AppropriateLiraNode узлов MIDAS.

           Проверяемые аспекты:
           1. Узел MIDAS находит соответствующий узел ЛИРА при совпадении X, Y, Z
           2. Свойство AppropriateLiraNode корректно обновляется
           3. Узлы без соответствия остаются с Id=0 в AppropriateLiraNode
        */
        [Fact]
        public void MapNodesAndElements_ShouldMapMatchingNodes()
        {
            // Arrange
            var midasNodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var liraNodes = new List<Mapper.LiraNodeInfo>
            {
                new Mapper.LiraNodeInfo(10, 0, 0, 0, new List<Mapper.LiraElementInfo>()),
                new Mapper.LiraNodeInfo(20, 2, 0, 0, new List<Mapper.LiraElementInfo>())
            };
            var midasElements = new List<Mapper.MidasElementInfo>();
            var liraElements = new List<Mapper.LiraElementInfo>();

            // Act
            Mapper.MapNodesAndElements(midasNodes, liraNodes, midasElements, liraElements);

            // Assert
            Assert.Equal(10, midasNodes[0].AppropriateLiraNode.Id);
            Assert.Equal(0, midasNodes[1].AppropriateLiraNode.Id);
        }

        /* Тест проверяет сопоставление элементов MIDAS с элементами ЛИРА-САПР.
           После сопоставления узлов, элементы MIDAS должны найти соответствующие элементы ЛИРА
           по списку идентификаторов узлов.

           Проверяемые аспекты:
           1. Элемент MIDAS находит соответствующий элемент ЛИРА по идентификаторам узлов
           2. Свойство AppropriateLiraElement корректно обновляется
           3. Сопоставление работает только если все узлы элемента найдены в ЛИРА
        */
        [Fact]
        public void MapNodesAndElements_ShouldMapMatchingElements()
        {
            // Arrange
            var midasNodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(3, 2, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var liraNodes = new List<Mapper.LiraNodeInfo>
            {
                new Mapper.LiraNodeInfo(10, 0, 0, 0, new List<Mapper.LiraElementInfo>()),
                new Mapper.LiraNodeInfo(20, 1, 0, 0, new List<Mapper.LiraElementInfo>()),
                new Mapper.LiraNodeInfo(30, 2, 0, 0, new List<Mapper.LiraElementInfo>())
            };
            var midasElements = new List<Mapper.MidasElementInfo>
            {
                new Mapper.MidasElementInfo(1, new[] { 1, 2 }, 0, 0, 0)
            };
            var liraElements = new List<Mapper.LiraElementInfo>
            {
                new Mapper.LiraElementInfo(100, new[] { 10, 20 })
            };

            // Act
            Mapper.MapNodesAndElements(midasNodes, liraNodes, midasElements, liraElements);

            // Assert
            Assert.Equal(100, midasElements[0].AppropriateLiraElement.Id);
        }

        /* Тест проверяет, что элементы не сопоставляются если их узлы не найдены в ЛИРА.
           Если хотя бы один узел элемента не имеет соответствия в ЛИРА,
           весь элемент не получает сопоставления.

           Проверяемые аспекты:
           1. Элемент MIDAS не получает сопоставление при неполном поиске узлов
           2. Свойство AppropriateLiraElement остается с дефолтным Id=0
           3. Метод продолжает работать корректно для остальных элементов
        */
        [Fact]
        public void MapNodesAndElements_ShouldNotMapElementsWhenNodesNotMapped()
        {
            // Arrange
            var midasNodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var liraNodes = new List<Mapper.LiraNodeInfo>
            {
                new Mapper.LiraNodeInfo(10, 1, 0, 0, new List<Mapper.LiraElementInfo>())
            };
            var midasElements = new List<Mapper.MidasElementInfo>
            {
                new Mapper.MidasElementInfo(1, new[] { 1, 2 }, 0, 0, 0)
            };
            var liraElements = new List<Mapper.LiraElementInfo>
            {
                new Mapper.LiraElementInfo(100, new[] { 10, 20 })
            };

            // Act
            Mapper.MapNodesAndElements(midasNodes, liraNodes, midasElements, liraElements);

            // Assert
            Assert.Equal(0, midasElements[0].AppropriateLiraElement.Id);
        }

        /* Тест проверяет создание одной пластины для связанных элементов.
           Метод ClusterizeElements использует BFS для объединения элементов,
           имеющих общие узлы, в один кластер (пластину).

           Проверяемые аспекты:
           1. Связанные элементы группируются в одну пластину
           2. Пластина содержит все элементы кластера
           3. Пластина содержит все уникальные узлы своих элементов
        */
        [Fact]
        public void ClusterizeElements_ShouldCreateSingleClusterForConnectedElements()
        {
            // Arrange
            var nodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(3, 2, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var elements = new List<Mapper.MidasElementInfo>
            {
                new Mapper.MidasElementInfo(1, new[] { 1, 2 }, 0, 0, 0),
                new Mapper.MidasElementInfo(2, new[] { 2, 3 }, 0, 0, 0)
            };

            // Act
            var plaques = Mapper.ClusterizeElements(elements, nodes);

            // Assert
            Assert.Single(plaques);
            Assert.Equal(2, plaques[0].Elements.Count);
            Assert.Equal(3, plaques[0].Nodes.Count);
        }

        /* Тест проверяет создание нескольких пластин для несвязанных элементов.
           Элементы без общих узлов должны быть сгруппированы в разные пластины.
           Используется алгоритм обхода в ширину (BFS) для поиска компонентов связности.

           Проверяемые аспекты:
           1. Создаются отдельные пластины для несвязанных элементов
           2. Каждая пластина содержит только свои элементы
           3. Количество пластин равно количеству несвязанных групп элементов
        */
        [Fact]
        public void ClusterizeElements_ShouldCreateMultipleClustersForDisconnectedElements()
        {
            // Arrange
            var nodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(3, 10, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(4, 11, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var elements = new List<Mapper.MidasElementInfo>
            {
                new Mapper.MidasElementInfo(1, new[] { 1, 2 }, 0, 0, 0),
                new Mapper.MidasElementInfo(2, new[] { 3, 4 }, 0, 0, 0)
            };

            // Act
            var plaques = Mapper.ClusterizeElements(elements, nodes);

            // Assert
            Assert.Equal(2, plaques.Count);
            Assert.Single(plaques[0].Elements);
            Assert.Single(plaques[1].Elements);
        }

        /* Тест проверяет присвоение пластины элементам и узлам.
           После кластеризации каждый элемент и узел получает ссылку
           на свою пластину через свойство Plaque.

           Проверяемые аспекты:
           1. Свойство Plaque каждого элемента не null
           2. Свойство Plaque каждого узла не null
           3. Id пластины корректно присваивается
        */
        [Fact]
        public void ClusterizeElements_ShouldAssignPlaqueToElementsAndNodes()
        {
            // Arrange
            var nodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var elements = new List<Mapper.MidasElementInfo>
            {
                new Mapper.MidasElementInfo(1, new[] { 1, 2 }, 0, 0, 0)
            };

            // Act
            var plaques = Mapper.ClusterizeElements(elements, nodes);

            // Assert
            Assert.NotNull(elements[0].Plaque);
            Assert.Equal(1, elements[0].Plaque.Id);
            Assert.NotNull(nodes[0].Plaque);
            Assert.NotNull(nodes[1].Plaque);
        }

        /* Тест проверяет обработку пустого списка элементов.
           Метод ClusterizeElements должен корректно работать
           при отсутствии элементов для обработки.

           Проверяемые аспекты:
           1. Метод не выбрасывает исключений для пустого списка
           2. Возвращается пустой список пластин
           3. Метод завершает работу корректно
        */
        [Fact]
        public void ClusterizeElements_ShouldHandleEmptyElementsList()
        {
            // Arrange
            var nodes = new List<Mapper.MidasNodeInfo>();
            var elements = new List<Mapper.MidasElementInfo>();

            // Act
            var plaques = Mapper.ClusterizeElements(elements, nodes);

            // Assert
            Assert.Empty(plaques);
        }

        /* Тест проверяет уникальность узлов в пластине.
           При добавлении узлов из нескольких элементов,
           дубликаты должны быть исключены методом Distinct().

           Проверяемые аспекты:
           1. Узлы, общие для нескольких элементов, включаются в пластину один раз
           2. Количество узлов в пластине соответствует количеству уникальных
           3. Метод использует Distinct() для исключения дубликатов
        */
        [Fact]
        public void ClusterizeElements_ShouldHaveUniqueNodes()
        {
            // Arrange
            var nodes = new List<Mapper.MidasNodeInfo>
            {
                new Mapper.MidasNodeInfo(1, 0, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(2, 1, 0, 0, 0, new List<Mapper.MidasElementInfo>()),
                new Mapper.MidasNodeInfo(3, 2, 0, 0, 0, new List<Mapper.MidasElementInfo>())
            };
            var elements = new List<Mapper.MidasElementInfo>
            {
                new Mapper.MidasElementInfo(1, new[] { 1, 2, 3 }, 0, 0, 0),
                new Mapper.MidasElementInfo(2, new[] { 2, 3 }, 0, 0, 0)
            };

            // Act
            var plaques = Mapper.ClusterizeElements(elements, nodes);

            // Assert
            Assert.Single(plaques);
            Assert.Equal(3, plaques[0].Nodes.Count);
        }
    }
}
