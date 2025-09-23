using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public class Writer
    {
        private readonly Parser _parser;

        public Writer(Parser parser)
        {
            _parser = parser;
        }

        /// <summary>
        /// Записывает коэффициенты постели и узловые жесткости в файл ЛИРА-САПР.
        /// </summary>
        public void WriteNodeAndBeddingData(string originalFilePath, double[] coefficientsC1, double[] nodeStiffnesses)
        {
            // Получаем обе карты позиций
            var positionsTuple = _parser.ParseTextFile(originalFilePath);
            var beddingPositions = positionsTuple.Item1;
            var stiffnessPositions = positionsTuple.Item2;

            // Считываем файл целиком
            string[] lines = File.ReadAllLines(originalFilePath);

            // Индексы жесткостей и коэффициентов
            int coefficientIndex = 0;
            int stiffnessIndex = 0;

            // Проходим по каждой строке файла
            for (int i = 0; i < lines.Length; i++)
            {
                // Если данная строка относится к коэффициентам постели
                if (beddingPositions.TryGetValue(i + 1, out string beddingTemplate))
                {
                    string replacementValue = coefficientsC1[coefficientIndex++].ToString();
                    lines[i] = beddingTemplate.Replace("<COEF_C1>", replacementValue);
                }

                // Если данная строка относится к узловым жесткостям
                if (stiffnessPositions.TryGetValue(i + 1, out string stiffnessTemplate))
                {
                    string replacementValue = nodeStiffnesses[stiffnessIndex++].ToString();
                    lines[i] = stiffnessTemplate.Replace("<STIFFNESS>", replacementValue);
                }
            }

            // Записываем исправленный файл обратно
            File.WriteAllLines(originalFilePath, lines);
        }
    }
}
