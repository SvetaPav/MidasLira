using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public class PositionFinder
    {
        /// <summary>
        /// Анализирует файл ЛИРА-САПР и находит позиции для вставки данных.
        /// </summary>
        public (int Section1EndPosition, int Section3EndPosition, int LastLinePosition) ParseTextFile(string filePath)
        {
            int section1EndPosition = -1;
            int section3EndPosition = -1;
            int lastLinePosition = -1;

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                int lineNumber = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    if (line.Contains("(1/") && section1EndPosition == -1)
                    {
                        // Начало раздела (1/)
                        section1EndPosition = FindEndOfSection(reader, lineNumber);
                    }
                    else if (line.Contains("(3/") && section3EndPosition == -1)
                    {
                        // Начало раздела (3/)
                        section3EndPosition = FindEndOfSection(reader, lineNumber);
                    }

                    // Обновляем последнюю строку файла
                    lastLinePosition = lineNumber;
                }
            }

            return (section1EndPosition, section3EndPosition, lastLinePosition);
        }

        // Метод для поиска конца раздела
        private int FindEndOfSection(StreamReader reader, int startLine)
        {
            string line;
            int lineNumber = startLine;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    return lineNumber; // Нашли пустую строку — конец раздела
                }
            }

            return -1; // Если не найдено, возвратим -1 (маловероятно)
        }
    }
}
