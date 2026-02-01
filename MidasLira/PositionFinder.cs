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
        /// Результат парсинга файла ЛИРА-САПР
        /// </summary>
        public class ParseResult
        {
            public int Section1Start { get; set; } = -1;  // Начало раздела (1/)
            public int Section1End { get; set; } = -1;    // Конец раздела (1/)
            public int Section3Start { get; set; } = -1;  // Начало раздела (3/)
            public int Section3End { get; set; } = -1;    // Конец раздела (3/)
            public int LastLine { get; set; } = -1;       // Последняя строка файла
        }


        /// <summary>
        /// Анализирует файл ЛИРА-САПР и находит позиции для вставки данных.
        /// Возвращает подробную информацию о разделах.
        /// </summary>
        public ParseResult ParseTextFile(string filePath)
        {
            var result = new ParseResult();

            // Проверка файла
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл не найден: {filePath}", filePath);

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                int lineNumber = 0;
                bool inSection1 = false;
                bool inSection3 = false;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    // Сохраняем последнюю строку
                    result.LastLine = lineNumber;

                    // Поиск начала раздела (1/)
                    if (line.Contains("(1/") && result.Section1Start == -1)
                    {
                        result.Section1Start = lineNumber;
                        inSection1 = true;
                        continue;
                    }

                    // Поиск начала раздела (3/)
                    if (line.Contains("(3/") && result.Section3Start == -1)
                    {
                        result.Section3Start = lineNumber;
                        inSection3 = true;
                        continue;
                    }

                    // Поиск конца раздела (1/)
                    if (inSection1 && IsSectionEnd(line))
                    {
                        result.Section1End = lineNumber;
                        inSection1 = false;
                    }

                    // Поиск конца раздела (3/)
                    if (inSection3 && IsSectionEnd(line))
                    {
                        result.Section3End = lineNumber;
                        inSection3 = false;
                    }

                    // Если достигнут конец файла, а раздел не закрыт
                    if (reader.EndOfStream)
                    {
                        if (inSection1) result.Section1End = lineNumber;
                        if (inSection3) result.Section3End = lineNumber;
                    }
                }
            }

            ValidateParseResult(result);
            return result;
        }

        /// <summary>
        /// Проверяет, является ли строка концом раздела
        /// </summary>
        private bool IsSectionEnd(string line)
        {
            // Конец раздела: пустая строка ИЛИ начало нового раздела
            return string.IsNullOrWhiteSpace(line) ||
                   (line.Contains("(/") && !line.Contains("(1/") && !line.Contains("(3/"));
        }

        /// <summary>
        /// Проверяет результат парсинга на корректность
        /// </summary>
        private void ValidateParseResult(ParseResult result)
        {
            var errors = new List<string>();

            if (result.Section1Start == -1)
                errors.Add("Не найден раздел (1/)");
            else if (result.Section1End == -1)
                errors.Add("Не найден конец раздела (1/)");
            else if (result.Section1Start >= result.Section1End)
                errors.Add($"Некорректные границы раздела (1/): начало={result.Section1Start}, конец={result.Section1End}");

            if (result.Section3Start == -1)
                errors.Add("Не найден раздел (3/)");
            else if (result.Section3End == -1)
                errors.Add("Не найден конец раздела (3/)");
            else if (result.Section3Start >= result.Section3End)
                errors.Add($"Некорректные границы раздела (3/): начало={result.Section3Start}, конец={result.Section3End}");

            if (result.LastLine == -1)
                errors.Add("Файл пуст");

            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    $"Ошибка парсинга файла ЛИРА-САПР:\n{string.Join("\n", errors)}");
            }
        }

        /// <summary>
        /// СТАРЫЙ МЕТОД для обратной совместимости
        /// </summary>
        [Obsolete("Используйте ParseTextFile, который возвращает ParseResult")]
        public (int Section1EndPosition, int Section3EndPosition, int LastLinePosition)
            ParseTextFileLegacy(string filePath)
        {
            var result = ParseTextFile(filePath);
            return (result.Section1End, result.Section3End, result.LastLine);
        }
    }
}
