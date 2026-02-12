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
    public static class Mapper  //GetNodesForElement - посмотреть об использовании логгера или сообщения об ошибке не через консоль
    {
        private const double COORDINATE_EPSILON = 0.001; // Погрешность для сравнения координат

        /// <summary>
        /// Строит соответствие элементов MIDAS и ЛИРА-САПР по общим узлам.
        /// </summary>

        public static void MapNodesAndElements(
            List<MidasNodeInfo> midasNodes,
            List<LiraNodeInfo> liraNodes,
            List<MidasElementInfo> midasElements,
            List<LiraElementInfo> liraElements)
        {
            // 1. Сопоставление узлов по координатам 
            foreach (var midasNode in midasNodes)
            {
                var matchingLiraNode = liraNodes.FirstOrDefault(l =>
                    Math.Abs(l.X - midasNode.X) < COORDINATE_EPSILON &&
                    Math.Abs(l.Y - midasNode.Y) < COORDINATE_EPSILON &&
                    Math.Abs(l.Z - midasNode.Z) < COORDINATE_EPSILON);

                if (!matchingLiraNode.IsEmpty)
                {
                    midasNode.AppropriateLiraNode = matchingLiraNode;
                }
            }

            // 2. СОЗДАЁМ СЛОВАРЬ УЗЛОВ MIDAS ДЛЯ БЫСТРОГО ДОСТУПА
            var midasNodeDict = midasNodes.ToDictionary(n => n.Id);

            // 3. Сопоставление элементов
            foreach (var midasElement in midasElements)
            {
                // Собираем ID узлов ЛИРА, соответствующих узлам MIDAS данного элемента
                var matchedNodeIds = midasElement.NodeIds
                    .Select(nodeId =>
                    {
                        // Поиск по словарю – O(1)
                        if (midasNodeDict.TryGetValue(nodeId, out var midasNode) &&
                            !midasNode.AppropriateLiraNode.IsEmpty) // ИСПРАВЛЕНО УСЛОВИЕ
                        {
                            return midasNode.AppropriateLiraNode.Id;
                        }
                        return 0;
                    })
                    .Where(id => id != 0)
                    .OrderBy(id => id)
                    .ToList();

                // Ищем элемент ЛИРА с точно таким же набором узлов
                var matchingLiraElement = liraElements.FirstOrDefault(le =>
                    le.NodeIds.OrderBy(id => id).SequenceEqual(matchedNodeIds));

                if (!matchingLiraElement.IsEmpty)
                {
                    midasElement.AppropriateLiraElement = matchingLiraElement;
                }
            }
        }


        /// <summary>
        /// Метод кластеризации элементов по плитам с использованием словаря узлов
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Plaque> ClusterizeElements(List<MidasElementInfo> elements, Dictionary<int, MidasNodeInfo> nodeDictionary)
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
                plaque.Nodes.AddRange(GetNodesForElement(element, nodeDictionary));

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
                            plaque.Nodes.AddRange(GetNodesForElement(otherElement, nodeDictionary));
                            queue.Enqueue(otherElement);
                            processedElements.Add(otherElement.Id);
                        }
                    }
                }
                plaque.Nodes = [.. plaque.Nodes.Distinct()];  // сокращение plaque.Nodes = plaque.Nodes.Distinct().ToList();

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
        private static List<MidasNodeInfo> GetNodesForElement(MidasElementInfo element, Dictionary<int, MidasNodeInfo> nodeDictionary)
        {
            var foundNodes = new List<MidasNodeInfo>(element.NodeIds.Length);

            foreach (var nodeId in element.NodeIds)
            {
                if (nodeDictionary.TryGetValue(nodeId, out var node))
                {
                    foundNodes.Add(node);
                }
                else
                {
                    AppLogger.Warning($"Узел с ID={nodeId} не найден для элемента ID={element.Id}");
                    // отладочная информация
                    Console.WriteLine($"Предупреждение: Узел с ID={nodeId} не найден для элемента ID={element.Id}");
                }
            }

            return foundNodes;
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
            public double NodeDisplacement { get; }
            public List<MidasElementInfo> Elements { get; }
            public LiraNodeInfo AppropriateLiraNode { get; set; } = LiraNodeInfo.Empty; // соответствующий узел в ЛИРА-САПР, используем Empty
            public Plaque Plaque { get; set; } // Номер плиты, к которой принадлежит узел
            public int RigidityNumber { get; set; } // Номер жесткости для записи в файл

            public MidasNodeInfo(int id, double x, double y, double z, double nodeDisplacement, List<MidasElementInfo>? elements = null)
            {
                Id = id;
                X = x;
                Y = y;
                Z = z;
                NodeDisplacement = nodeDisplacement;
                Elements = elements ?? []; // Используем новый синтаксис
                AppropriateLiraNode = LiraNodeInfo.Empty; // изначально не знаем соответствующий узел в ЛИРА-САПР
                Plaque = new Plaque();
                RigidityNumber = 0; // Изначально не задан
            }
        }

        public class MidasElementInfo
        {
            public int Id { get; }
            public int[] NodeIds { get; } // Узлы, принадлежащие элементу
            public double Stress { get; set; } // Напряжение в элементе
            public double Displacement { get; } // Перемещение элемента
            public double BeddingCoefficient { get; set; } // Коэффициент постели (C1)
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

        public readonly struct LiraNodeInfo: IEquatable<LiraNodeInfo>
        {
            private const double Epsilon = 1e-6;  // Допуск 0.001 мм
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
                Elements = elements ?? [];
            }

            // Cтатическое свойство для "пустого" значения
            public static LiraNodeInfo Empty => new(0, 0, 0, 0, []);

            // Метод для проверки на "пустоту"
            public bool IsEmpty => Id == 0;

            // Реализация IEquatable для избежания боксинга
            public bool Equals (LiraNodeInfo other)
            {
                return Id == other.Id && 
                    Math.Abs(X - other.X) < Epsilon &&
                    Math.Abs (Y - other.Y) < Epsilon &&
                    Math.Abs (Z- other.Z) < Epsilon;
            }

            // Переопределяем Equals для корректного сравнения
            public override bool Equals(object? obj)
            {
                return obj is LiraNodeInfo other && Equals(other);
            }

            // GetHashCode должен быть согласован с Equals
            public override int GetHashCode()
            {
                // Округляем координаты для хэш-кода
                int xHash = (int)(X / Epsilon);
                int yHash = (int)(Y / Epsilon);
                int zHash = (int)(Z / Epsilon);

                return HashCode.Combine(Id, xHash, yHash, zHash);
            }

            // Перегрузка операторов == и !=
            public static bool operator ==(LiraNodeInfo left, LiraNodeInfo right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(LiraNodeInfo left, LiraNodeInfo right)
            {
                return !(left == right);
            }
        }

        public readonly struct LiraElementInfo
        {
            public int Id { get; }
            public int[] NodeIds { get; } // Узлы, принадлежащие элементу

            public LiraElementInfo(int id, int[] nodeIds)
            {
                Id = id;
                NodeIds = nodeIds ?? [];
            }
            public static LiraElementInfo Empty => new(0, []);
            public bool IsEmpty => Id == 0;
        }

        public class Plaque
        {
            public int Id { get; set; } = 0;
            public List<MidasElementInfo> Elements { get; set; } = []; // Элементы, принадлежащие плите
            public List<MidasNodeInfo> Nodes { get; set; } = []; // Узлы, принадлежащие плите
            public double RigidNodes { get; set; } = 0; // жесткоcть для узлов, принадлежащих этой плите

        }
    }
}

