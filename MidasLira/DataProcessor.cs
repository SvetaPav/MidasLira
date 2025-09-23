using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public class DataProcessor
    {
        private readonly Parser _parser;
        private readonly Calculator _calculator;
        private readonly Writer _writer;

        public DataProcessor(Parser parser, Calculator calculator, Writer writer)
        {
            _parser = parser;
            _calculator = calculator;
            _writer = writer;
        }

        /// <summary>
        /// Главная точка входа для обработки данных.
        /// </summary>
        public bool ProcessFile(string excelFilePath, string liraSaprFilePath)
        {
            try
            {
                // Шаг 1: Парсим файл ЛИРА-САПР
                var parsedData = _parser.ParseTextFile(liraSaprFilePath);

                // Шаг 2: Читаем данные из Excel
                var excelData = ReadFromExcel(excelFilePath);

                // Шаг 3: Передаем данные калькулятору для расчета
                var calculatedResults = _calculator.CalculateParameters(excelData);

                // Шаг 4: Записываем узловые жесткости в файл ЛИРА-САПР
                _writer.WriteNodeAndBeddingData(liraSaprFilePath, calculatedResults.StiffnessValues, calculatedResults.NodeStiffnesses);

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при обработке данных.", ex);
            }
        }

        /// <summary>
        /// Читает данные из файла Excel.
        /// </summary>
        private object[] ReadFromExcel(string path)
        {
            // Тут предполагается реализация чтения данных из Excel,
            // например, с использованием библиотеки NPOI или EPPlus.
            // Пока возвращаем заглушку.
            return new object[] { "Dummy data" };
        }
    }
}
