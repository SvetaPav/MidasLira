using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MidasLira
{
    public class PositionFinder
    {
        /// <summary>
        /// Результат парсинга файла ЛИРА-САПР
        /// </summary>
        public class ParseResult
        {
            // Раздел (1/) - элементы
            public int Section1Start { get; set; } = -1;      // Строка с "( 1/"
            public int Section1End { get; set; } = -1;        // Строка с ")"
            public int Section1LastElementLine { get; set; } = -1; // Последняя строка с элементами перед ")"

            // Раздел (3/) - материалы и жесткости
            public int Section3Start { get; set; } = -1;      // Строка с "( 3/"
            public int Section3End { get; set; } = -1;        // Строка с ")"
            public int Section3LastMaterialLine { get; set; } = -1; // Последняя строка материалов (S0/GEI)

            // Раздел (17/) - последний раздел в файле
            public int Section17Start { get; set; } = -1;     // Строка с "( 17/"
            public int Section17End { get; set; } = -1;       // Строка с ")" - КОНЕЦ ФАЙЛА!

            // Раздел (19/) - коэффициенты постели (может отсутствовать)
            public int Section19Start { get; set; } = -1;     // Строка с "( 19/"
            public int Section19End { get; set; } = -1;       // Строка с ")"
            public int Section19LastCoefficientLine { get; set; } = -1; // Последний коэффициент

            public int FileEnd { get; set; } = -1;            // Последняя строка файла
        }

        /// <summary>
        /// Анализирует файл ЛИРА-САПР для определения мест вставки данных
        /// </summary>
        public ParseResult ParseTextFile(string filePath)
        {
            var result = new ParseResult();

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл не найден: {filePath}", filePath);

            var lines = File.ReadAllLines(filePath);
            result.FileEnd = lines.Length;

            bool inSection1 = false;
            bool inSection3 = false;
            bool inSection17 = false;
            bool inSection19 = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // ===== НАЧАЛО РАЗДЕЛА (1/) =====
                if (line.StartsWith("( 1/"))
                {
                    result.Section1Start = i + 1; // +1 для 1-индексации
                    inSection1 = true;
                    result.Section1LastElementLine = i + 1; // Пока что начало раздела
                    continue;
                }

                // ===== НАЧАЛО РАЗДЕЛА (3/) =====
                if (line.StartsWith("( 3/"))
                {
                    result.Section3Start = i + 1;
                    inSection3 = true;
                    result.Section3LastMaterialLine = i + 1; // Пока что начало раздела
                    continue;
                }

                // ===== НАЧАЛО РАЗДЕЛА (17/) =====
                if (line.StartsWith("( 17/"))
                {
                    result.Section17Start = i + 1;
                    inSection17 = true;
                    continue;
                }

                // ===== НАЧАЛО РАЗДЕЛА (19/) =====
                if (line.StartsWith("( 19/"))
                {
                    result.Section19Start = i + 1;
                    inSection19 = true;
                    result.Section19LastCoefficientLine = i + 1; // Пока что начало раздела
                    continue;
                }

                // ===== ОБРАБОТКА ВНУТРИ РАЗДЕЛА (1/) =====
                if (inSection1)
                {
                    // Если это строка с элементом (44, 42, 56 и т.д.)
                    if (IsElementLine(line))
                    {
                        result.Section1LastElementLine = i + 1;
                    }

                    // Конец раздела (1/)
                    if (line == ")")
                    {
                        result.Section1End = i + 1;
                        inSection1 = false;
                    }
                }

                // ===== ОБРАБОТКА ВНУТРИ РАЗДЕЛА (3/) =====
                if (inSection3)
                {
                    // Если это строка материала (S0, GEI, RO, Mu)
                    if (IsMaterialLine(line))
                    {
                        result.Section3LastMaterialLine = i + 1;
                    }

                    // Конец раздела (3/)
                    if (line == ")")
                    {
                        result.Section3End = i + 1;
                        inSection3 = false;
                    }
                }

                // ===== ОБРАБОТКА ВНУТРИ РАЗДЕЛА (17/) =====
                if (inSection17)
                {
                    // Конец раздела (17/)
                    if (line == ")")
                    {
                        result.Section17End = i + 1;
                        inSection17 = false;
                    }
                }

                // ===== ОБРАБОТКА ВНУТРИ РАЗДЕЛА (19/) =====
                if (inSection19)
                {
                    // Если это строка с коэффициентом постели
                    if (IsCoefficientLine(line))
                    {
                        result.Section19LastCoefficientLine = i + 1;
                    }

                    // Конец раздела (19/)
                    if (line == ")")
                    {
                        result.Section19End = i + 1;
                        inSection19 = false;
                    }
                }
            }

            // Проверяем, что раздел (17/) есть (он всегда должен быть)
            if (result.Section17Start == -1)
            {
                throw new InvalidDataException("В файле отсутствует обязательный раздел (17/) - файл может быть поврежден");
            }

            ValidateParseResult(result, lines);
            return result;
        }

        private bool IsElementLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line == ")" || line.StartsWith("("))
                return false;

            // Элементы начинаются с цифр (44, 42, 56 и т.д.)
            var firstToken = line.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(firstToken))
                return false;

            return int.TryParse(firstToken, out _);
        }

        private bool IsMaterialLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            // Материалы: S0, GEI, 0 RO, 0 Mu
            return line.StartsWith("S0") || line.StartsWith("GEI") ||
                   line.StartsWith("0 RO") || line.StartsWith("0 Mu");
        }

        private bool IsCoefficientLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line == ")" || line.StartsWith("("))
                return false;

            var parts = line.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            // Проверяем, что первая часть - число (ID элемента)
            if (!int.TryParse(parts[0], out _))
                return false;

            // Проверяем, что вторая часть - число (коэффициент)
            return double.TryParse(parts[1], out _);
        }

        private void ValidateParseResult(ParseResult result, string[] lines)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // КРИТИЧЕСКИЕ ОШИБКИ
            if (result.Section1Start == -1)
                errors.Add("Не найден раздел (1/) - обязательный раздел!");
            else if (result.Section1End == -1)
                errors.Add("Не найден конец раздела (1/)");

            if (result.Section3Start == -1)
                warnings.Add("Раздел (3/) не найден, будет создан новый");
            else if (result.Section3End == -1)
                errors.Add("Не найден конец раздела (3/)");

            if (result.Section17Start == -1)
                errors.Add("Не найден раздел (17/) - обязательный раздел!");
            else if (result.Section17End == -1)
                errors.Add("Не найден конец раздела (17/)");

            if (result.Section19Start != -1 && result.Section19End == -1)
                errors.Add("Не найден конец раздела (19/)");

            // ПРОВЕРКА ПОРЯДКА РАЗДЕЛОВ
            if (result.Section19Start != -1 && result.Section17Start != -1)
            {
                if (result.Section19Start < result.Section17End)
                {
                    errors.Add("Раздел (19/) находится перед разделом (17/), но должен быть после!");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    $"Ошибка парсинга файла ЛИРА-САПР:\n{string.Join("\n", errors)}" +
                    (warnings.Count > 0 ? $"\nПредупреждения:\n{string.Join("\n", warnings)}" : ""));
            }

            if (warnings.Count > 0)
            {
                Console.WriteLine($"Предупреждения: {string.Join("; ", warnings)}");
            }
        }

        /// <summary>
        /// Находит позицию для создания/вставки раздела (19/)
        /// </summary>
        public int FindPositionForSection19(ParseResult parseResult)
        {
            // Если раздел (19/) уже есть - возвращаем позицию для добавления новых коэффициентов
            if (parseResult.Section19Start != -1)
            {
                return parseResult.Section19LastCoefficientLine;
            }

            // Если раздела (19/) нет - создаем его ПОСЛЕ раздела (17/)
            // Вставляем сразу после закрывающей скобки раздела (17/)
            return parseResult.Section17End;
        }
    }
}