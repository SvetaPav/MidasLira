using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static MidasLira.Mapper;

namespace MidasLira
{
    public class RigidityCalculator
    {

        /// <summary>
        /// Вычисляет жесткость узлов, основываясь на среднем значении C1 и площади плиты
        /// </summary>
        /// <param name="nodes">Список узлов MIDAS</param>
        /// <param name="elements">Список элементов MIDAS</param>
        /// <returns>Список плит с рассчитанными жесткостями</returns>

        public List<Plaque> CalculateNodeRigidities(List<MidasNodeInfo> nodes, List<MidasElementInfo> elements)
        {
            // ПРОВЕРКА: Входные коллекции
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (nodes.Count == 0) throw new ArgumentException("Список узлов не может быть пустым.", nameof(nodes));
            if (elements.Count == 0) throw new ArgumentException("Список элементов не может быть пустым.", nameof(elements));

            // СОЗДАЕМ СЛОВАРЬ для быстрого поиска узлов
            var nodeDictionary = nodes.ToDictionary(n => n.Id, n => n);
           
            return CalculateNodeRigiditiesWithDictionary(nodeDictionary, elements);
        }

        /// <summary>
        /// Приватный метод для расчета жесткостей с использованием словаря узлов
        /// </summary>

        private List<Plaque> CalculateNodeRigiditiesWithDictionary(
           Dictionary<int, MidasNodeInfo> nodeDictionary,
           List<MidasElementInfo> elements)
        {
            // Проверка словаря
            if (nodeDictionary == null) throw new ArgumentNullException(nameof(nodeDictionary));
            if (nodeDictionary.Count == 0) throw new ArgumentException("Словарь узлов не может быть пустым.", nameof(nodeDictionary));
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (elements.Count == 0) throw new ArgumentException("Список элементов не может быть пустым.", nameof(elements));

            // Группа элементов по плитам (теперь передаем словарь)
            var plaques = ClusterizeElements(elements, nodeDictionary);

            // Проходим по каждой плите
            for (int i = 0; i < plaques.Count; i++)
            {
                var plaque = plaques[i];

                // Средний коэффициент постели для данной плиты
                var avgC1 = AverageBeddingCoefficient(plaque.Elements);

                // Площадь плиты
                var area = GetPlaqueArea(plaque.Elements, nodeDictionary);

                // Жесткость плиты (общая для всех узлов плиты)
                plaque.RigidNodes = (area * avgC1 * 0.7) / plaque.Elements.Count;
            }

            return plaques;
        }

        // Метод расчета площади плиты
        private double GetPlaqueArea(List<MidasElementInfo> plaque, Dictionary<int, MidasNodeInfo> nodeDictionary)
        {
            return plaque.Sum(element => CalculateElementArea(element, nodeDictionary));
        }


        // Метод для расчета площади элемента 
        private double CalculateElementArea(MidasElementInfo element, Dictionary<int, MidasNodeInfo> nodeDictionary)
        {
            // ПРОВЕРКА: Элемент и узлы
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (nodeDictionary == null) throw new ArgumentNullException(nameof(nodeDictionary));

            // Получаем координаты узлов элемента
            var points = new List<MidasNodeInfo>();
            foreach (var nodeID in element.NodeIds)
            {
                if (!nodeDictionary.TryGetValue(nodeID, out var node))
                {
                    throw new InvalidOperationException($"Для элемента ID={element.Id} узел {nodeID} не найден в общем списке.");
                }
                points.Add(node);
            }


            return points.Count switch
            {
                // Треугольник
                3 => TriangleArea(points[0], points[1], points[2]),
                // Четырехугольник
                4 => QuadrilateralArea(points[0], points[1], points[2], points[3]),
                _ => throw new ArgumentException($"Некорректное количество узлов элемента ID={element.Id}: {points.Count}. Ожидается 3 или 4."),
            };
        }

        // Метод для расчета площади треугольника
        private double TriangleArea(MidasNodeInfo A, MidasNodeInfo B, MidasNodeInfo C)
        {
            if (A == null || B == null || C == null)
                throw new ArgumentNullException("Все параметры треугольника должны быть не null");

            double sideA = Distance(A, B);
            double sideB = Distance(B, C);
            double sideC = Distance(C, A);

            double s = (sideA + sideB + sideC) / 2;
            return Math.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));
        }

        // Метод для расчета площади четырехугольника (деление на два треугольника)
        private double QuadrilateralArea(MidasNodeInfo A, MidasNodeInfo B, MidasNodeInfo C, MidasNodeInfo D)
        {
            if (A == null || B == null || C == null)
                throw new ArgumentNullException("Все параметры четырехугольника должны быть не null");

            return TriangleArea(A, B, C) + TriangleArea(A, C, D);
        }

        // Метод для расчета среднего коэффициента постели
        private double AverageBeddingCoefficient(List<MidasElementInfo> elements)
        {
            if (elements == null || elements.Count == 0)
                return 0;
            return elements.Sum(e => e.BeddingCoefficient) / elements.Count;
        }     
   
        // Расстояние между двумя узлами.     
        public static double Distance(MidasNodeInfo a, MidasNodeInfo b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException("Оба узла должны быть не null");

            return Math.Sqrt(Math.Pow(a.X - b.X, 2) +
                           Math.Pow(a.Y - b.Y, 2) +
                           Math.Pow(a.Z - b.Z, 2));
        }
    }
}
