using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace MidasLira
{
    public static class Mapper
    {
        private const double Epsilon = 1e-6; // Погрешность для сравнения координат, мб надо будет

        /// <summary>
        /// Строит соответствие элементов MIDAS и ЛИРА-САПР по общим узлам.
        /// </summary>
        public static void MapNodesAndElements(List<MidasNodeInfo> midasNodes, List<LiraNodeInfo> liraNodes, List<MidasElementInfo> midasElements, List<LiraElementInfo> liraElements)
        {
            // 1. Сопоставляем узлы 
            for (int i = 0; i < midasNodes.Count; i++)
            {
                var midasNode = midasNodes[i];
                var matchingLiraNode = liraNodes.FirstOrDefault(l =>
                    l.X == midasNode.X &&
                    l.Y == midasNode.Y &&
                    l.Z == midasNode.Z);

                if (matchingLiraNode.Id != 0)
                {
                    midasNode.AppropriateLiraNode = matchingLiraNode;
                }
            }

            // 2. Сопоставляем элементы на основе сопоставленных узлов
            for (int i = 0; i < midasElements.Count; i++)
            {
                var midasElement = midasElements[i];
                // Получить сопоставленные узлы для текущего элемента Midas
                var matchedNodeIds = midasElement.NodeIds
                                    .Select(midNodeId =>
                                    {
                                        var correspondingNode = midasNodes.FirstOrDefault(md => md.Id == midNodeId);
                                        if (correspondingNode != null && correspondingNode.AppropriateLiraNode.Id != 0)
                                            return correspondingNode.AppropriateLiraNode.Id;
                                        return 0;
                                    })
                                    .Where(id => id != 0)
                                    .OrderBy(id => id)
                                    .ToList();

                // Ищем элемент ЛИРА-САПР, чьи узлы совпадают с сопоставленными узлами
                var matchingLiraElement = liraElements.FirstOrDefault(le =>
                    le.NodeIds.OrderBy(id => id).SequenceEqual(matchedNodeIds));

                if (matchingLiraElement.Id != 0) // Проверяем, что элемент реально найден
                {
                    midasElement.AppropriateLiraElement = matchingLiraElement;
                }
            }
        }


        /// <summary>
        /// Метод кластеризации элементов по плитам
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Plaque> ClusterizeElements(List<MidasElementInfo> elements, List<MidasNodeInfo> nodes)
        {
            var plaques = new List<Plaque>();

            // Набор элементов, которые уже учтены в плитах
            var processedElements = new HashSet<int>();

            foreach (var element in elements)
            {
                if (processedElements.Contains(element.Id))
                    continue; // Элемент уже включен в кластер

                // Создаем новую плиту
                var plaque = new Plaque();
                plaque.Elements.Add(element);
                plaque.Nodes.AddRange(GetNodesForElement(element, nodes));

                var queue = new Queue<MidasElementInfo>();
                queue.Enqueue(element);
                processedElements.Add(element.Id);

                // DFS-обход смежных элементов
                while (queue.Count > 0) //графовый поиск, где элементы — вершины графа, а общие узлы — рёбра.
                {
                    var currentElement = queue.Dequeue();

                    // Просматриваем все элементы
                    foreach (var otherElement in elements)
                    {
                        if (processedElements.Contains(otherElement.Id))
                            continue; // Элемент уже учтен

                        // Если элементы имеют хотя бы один общий узел
                        if (otherElement.NodeIds.Intersect(currentElement.NodeIds).Any())
                        {
                            plaque.Elements.Add(otherElement);
                            plaque.Nodes.AddRange(GetNodesForElement(otherElement, nodes));
                            queue.Enqueue(otherElement);
                            processedElements.Add(otherElement.Id);
                        }
                    }
                }
                plaque.Nodes = plaque.Nodes.Distinct().ToList();

                // Присваиваем плите уникальный номер
                plaque.Id = plaques.Count + 1;

                // Заполняем поле PlankId в элементах и узлах
                for (int i = 0; i < plaque.Elements.Count; i++)
                {
                    plaque.Elements[i].Plaque = plaque;
                }
                for (int j = 0; j < plaque.Nodes.Count; j++)
                {
                    plaque.Nodes[j].Plaque = plaque;
                }

                plaques.Add(plaque);
            }

            return plaques;
        }

        // Вспомогательный метод для получения узлов элемента
        private static IEnumerable<MidasNodeInfo> GetNodesForElement(MidasElementInfo element, List<MidasNodeInfo> nodes)
        {
            return element.NodeIds.Select(nodeId => nodes.FirstOrDefault(n => n.Id == nodeId)).Where(n => n != null);
        }
        //Как работает метод:
        //Initialization:  
        //Создаются два главных объекта:
        //plaques: список, в который будут собираться готовые плиты.
        //processedElements: хешсет, в котором хранятся идентификаторы элементов, уже вошедших в какую-либо плиту.
        //Outer Loop:Метод проходит по каждому элементу из списка elements. Если элемент уже включён в плиту (проверяется по хешсету processedElements), он пропускается.
        //Creating a plaques:  
        //Для каждого нового элемента создаётся новая плита (список элементов), и этот элемент добавляется в очередь (queue).
        //Затем элемент добавляется в хешсет processedElements, чтобы отметить, что он уже учтен.
        //DFS Traversal:  
        //Используя очередь (queue), мы проходим по всем связанным элементам. Это своего рода графовый поиск, где элементы — вершины графа, а общие узлы — рёбра.
        //Пока очередь не пуста, мы достаём элемент из очереди и проверяем все остальные элементы в списке.
        //Если найден элемент, имеющий хотя бы один общий узел с текущим элементом, он добавляется в текущую плиту, в очередь и отмечается как обработанный.
        //Adding Clusters:  
        //После того как найдены все связанные элементы, текущая плита добавляется в список plaques.
        // Repeat:Процедура повторяется для оставшихся необработанных элементов.


        // Вспомогательные структуры
        public class MidasNodeInfo
        {
            public int Id { get; }
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public List<MidasElementInfo> Elements { get; }
            public LiraNodeInfo AppropriateLiraNode { get; set; }  // соответствующий узел в ЛИРА-САПР
            public Plaque Plaque { get; set; } // Номер плиты, к которой принадлежит узел

            public MidasNodeInfo(int id, double x, double y, double z, List<MidasElementInfo> elements)
            {
                Id = id;
                X = x;
                Y = y;
                Z = z;
                Elements = elements;
                AppropriateLiraNode = new LiraNodeInfo(); // изначально не знаем соответствующий узел в ЛИРА-САПР
                Plaque = new Plaque();
            }
        }

        public class MidasElementInfo
        {
            public int Id { get; }
            public int[] NodeIds { get; } // Узлы, принадлежащие элементу
            public double Stress { get; set; } // Напряжение в элементе
            public double Displacement { get; } // Перемещение элемента
            public double BeddingCoefficient { get; } // Коэффициент постели (C1)
            public int PlankId { get; set; } // Номер плиты, к которой принадлежит элемент
            public LiraElementInfo AppropriateLiraElement { get; set; }
            public Plaque Plaque { get; set; }

            public MidasElementInfo(int id, int[] nodeIds, double stress, double displacement, double beddingCoefficient)
            {
                Id = id;
                NodeIds = nodeIds;
                Stress = stress;
                Displacement = displacement;
                BeddingCoefficient = beddingCoefficient;
                PlankId = -1; // Изначально элемент не прикреплён ни к какой плите
                AppropriateLiraElement = new LiraElementInfo(); // изначально не знаем соответствующий элемент в ЛИРА-САПР
                Plaque = new Plaque();
            }
        }

        public struct LiraNodeInfo
        {
            public int Id { get; }
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public List<LiraElementInfo> Elements { get; }

            public LiraNodeInfo(int id, double x, double y, double z, List<LiraElementInfo> elements)
            {
                Id = id;
                X = x;
                Y = y;
                Z = z;
                Elements = elements;
            }
        }

        public struct LiraElementInfo
        {
            public int Id { get; }
            public int[] NodeIds { get; } // Узлы, принадлежащие элементу

            public LiraElementInfo(int id, int[] nodeIds)
            {
                Id = id;
                NodeIds = nodeIds;
            }
        }

        public class Plaque
        {
            public int Id { get; set; }
            public List<MidasElementInfo> Elements { get; set; } // Элементы, принадлежащие плите
            public List<MidasNodeInfo> Nodes { get; set; } // Узлы, принадлежащие плите
            public double rigidNodes { get; set; }  // жесткоcть для узлов, принадлежащих этой плите

            public Plaque()
            {
                Id = 0;
                Elements = new List<MidasElementInfo>();
                Nodes = new List<MidasNodeInfo>();
                rigidNodes = 0;
            }
        }
    }
}