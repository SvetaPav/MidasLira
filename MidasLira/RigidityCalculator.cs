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
        /// Вычисляет жесткость узлов, основываясь на среднем значении C1 и площади плиты, и записывает в структуру Plaque
        /// </summary>
        public List<Plaque> CalculateNodeRigidities(List<MidasNodeInfo> nodes, List<MidasElementInfo> elements)
        {
            // Группа элементов по плитам
            var plaques = ClusterizeElements(elements, nodes);

            // Проходим по каждой плите
            for (int i = 0; i < plaques.Count; i++)
            {
                var plaque = plaques[i];

                // Средний коэффициент постели для данной плиты
                var avgC1 = AverageBeddingCoefficient(plaque.Elements);

                // Площадь плиты
                var area = GetPlaqueArea(plaque.Elements, nodes);

                // Жесткость плиты (общая для всех узлов плиты)
                plaque.rigidNodes = (area * avgC1 * 0.7) / plaque.Elements.Count;
            }
            return plaques;
        }


        // Метод расчета площади плиты
        private double GetPlaqueArea(List<MidasElementInfo> plaque, List<MidasNodeInfo> nodes)
        {
            return plaque.Sum(element => CalculateElementArea(element, nodes));
        }


        // Метод для расчета площади элемента 
        private double CalculateElementArea(MidasElementInfo element, List<MidasNodeInfo> nodes)
        {
            // Получаем координаты узлов элемента
            var points = element.NodeIds.Select(nodeId => nodes.FirstOrDefault(n => n.Id == nodeId)).ToList();

            switch (points.Count)
            {
                case 3: // Треугольник
                    return TriangleArea(points[0], points[1], points[2]);

                case 4: // Четырехугольник
                    return QuadrilateralArea(points[0], points[1], points[2], points[3]);

                default:
                    throw new ArgumentException("Некорректное количество узлов элемента.");
            }
        }

        // Метод для расчета площади треугольника
        private double TriangleArea(MidasNodeInfo A, MidasNodeInfo B, MidasNodeInfo C)
        {
            double sideA = Distance(A, B);
            double sideB = Distance(B, C);
            double sideC = Distance(C, A);

            double s = (sideA + sideB + sideC) / 2;
            return Math.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));
        }

        // Метод для расчета площади четырехугольника (деление на два треугольника)
        private double QuadrilateralArea(MidasNodeInfo A, MidasNodeInfo B, MidasNodeInfo C, MidasNodeInfo D)
        {
            return TriangleArea(A, B, C) + TriangleArea(A, C, D);
        }

        // Метод для расчета среднего коэффициента постели
        private double AverageBeddingCoefficient(List<MidasElementInfo> elements)
        {
            return elements.Sum(e => e.BeddingCoefficient) / elements.Count;
        }     
   
        // Расстояние между двумя узлами.     
        public static double Distance(MidasNodeInfo a, MidasNodeInfo b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) +
                           Math.Pow(a.Y - b.Y, 2) +
                           Math.Pow(a.Z - b.Z, 2));
        }
    }
}
