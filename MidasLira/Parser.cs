using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public class Parser
    {
        /// <summary>
        /// Анализирует файл ЛИРА-САПР и формирует карты позиций для замещения данных.
        /// </summary>
        public Tuple<Dictionary<int, string>, Dictionary<int, string>> ParseTextFile(string filePath)
        {
            var beddingPositions = new Dictionary<int, string>(); // Карта для коэффициентов постели
            var stiffnessPositions = new Dictionary<int, string>(); // Карта для узловых жесткостей

            // Открываем файл для чтения
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                int currentLineNumber = 0;

                while ((line = sr.ReadLine()) != null)
                {
                    currentLineNumber++;

                    // Обнаруживаем позицию для коэффициентов постели
                    if (line.Contains("BEDDING_COEFFICIENT:"))
                    {
                        beddingPositions[currentLineNumber] = line.Replace("BEDDING_COEFFICIENT:", "<COEF_C1>");
                    }

                    // Обнаруживаем позицию для узловых жесткостей
                    if (line.Contains("NODE_STIFFNESS:"))
                    {
                        stiffnessPositions[currentLineNumber] = line.Replace("NODE_STIFFNESS:", "<STIFFNESS>");
                    }
                }
            }

            return new Tuple<Dictionary<int, string>, Dictionary<int, string>>(beddingPositions, stiffnessPositions);
        }
    }
}
