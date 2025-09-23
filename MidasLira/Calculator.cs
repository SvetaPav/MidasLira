using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public class Calculator
    {
        /// <summary>
        /// Выполняет расчет коэффициентов постели C1 и узловых жесткостей.
        /// </summary>
        public CalculationResult CalculateParameters(object[] inputData)
        {
            double[] stiffnessValues = ExtractStiffnessValues(inputData);
            double[] nodeStiffnesses = CalculateNodeStiffness(stiffnessValues);

            return new CalculationResult
            {
                StiffnessValues = stiffnessValues,
                NodeStiffnesses = nodeStiffnesses
            };
        }

        /// <summary>
        /// Вычисляем коэффициенты постели C1.
        /// </summary>
        private double[] CalculateC1(double[] stiffnessValues)
        {
            // Примитивный пример формулы для расчета C1.
            // Реальный расчет будет зависеть от конкретной физической модели.
            double[] c1Values = new double[stiffnessValues.Length];
            for (int i = 0; i < stiffnessValues.Length; i++)
            {
                c1Values[i] = stiffnessValues[i] / Math.Sqrt(i + 1); // Упрощённая формула
            }
            return c1Values;
        }

        /// <summary>
        /// Вычисляем узловые жесткости на основе коэффициентов постели C1.
        /// </summary>
        private double[] CalculateNodeStiffness(double[] stiffnessValues)
        {
            double[] c1Values = CalculateC1(stiffnessValues);
            double[] nodeStiffnesses = new double[c1Values.Length];
            for (int i = 0; i < c1Values.Length; i++)
            {
                nodeStiffnesses[i] = c1Values[i]; // Узловая жесткость равна C1
            }
            return nodeStiffnesses;
        }

        /// <summary>
        /// Извлекает значения жесткости из массива данных.
        /// </summary>
        private double[] ExtractStiffnessValues(object[] inputData)
        {
            // Предполагается, что inputData содержит массив двойных значений (double),
            // где каждое значение - это начальная жесткость.
            double[] stiffnessValues = Array.ConvertAll(inputData, x => Convert.ToDouble(x));
            return stiffnessValues;
        }
    }

    public class CalculationResult
    {
        public double[] StiffnessValues { get; set; }
        public double[] NodeStiffnesses { get; set; }
    }
}
