using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MidasLira.Mapper;

namespace MidasLira
{
    public class DataProcessor
    {
        private readonly Writer _writer;
        private readonly RigidityCalculator _rigidityCalculator;
        private readonly ExcelReader _excelReader;

        public DataProcessor(RigidityCalculator rigidityCalculator, Writer writer, ExcelReader excelReader)
        {
            // ПРОВЕРКА: Внедряемые зависимости не должны быть null
            _rigidityCalculator = rigidityCalculator ?? throw new ArgumentNullException(nameof(rigidityCalculator));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _excelReader = excelReader ?? throw new ArgumentNullException(nameof(excelReader));
        }

        /// <summary>
        /// Главный метод обработки данных.
        /// </summary>
        public bool ProcessFile(string excelFilePath, string liraSaprFilePath)
        {
            // ПРОВЕРКА: Входные файлы
            if (string.IsNullOrWhiteSpace(excelFilePath))
                throw new ArgumentException("Путь к файлу Excel не может быть пустым.", nameof(excelFilePath));
            if (string.IsNullOrWhiteSpace(liraSaprFilePath))
                throw new ArgumentException("Путь к файлу ЛИРА-САПР не может быть пустым.", nameof(liraSaprFilePath));


            List<MidasNodeInfo> midasNodes = new List<MidasNodeInfo>();
            List<LiraNodeInfo> liraNodes = new List<LiraNodeInfo>();
            List<MidasElementInfo> midasElements = new List<MidasElementInfo>();
            List<LiraElementInfo> liraElements = new List<LiraElementInfo>();
            List<Plaque> plaques = new List<Plaque>();

            try
            {
                // Шаг 1: Чтение данных из Excel
                (midasNodes, liraNodes, midasElements, liraElements) = _excelReader.ReadFromExcel(excelFilePath);

                // Шаг 2: Сопоставление узлов и элементов, запись в соответственные классы
                MapNodesAndElements(midasNodes, liraNodes, midasElements, liraElements);

                // Шаг 3: Расчет коэффициентов постели и жесткостей узлов, запись в соответственные классы
                plaques = _rigidityCalculator.CalculateNodeRigidities(midasNodes, midasElements);

                // Шаг 4: Запись данных в файл ЛИРА-САПР
                _writer.WriteNodeAndBeddingData(liraSaprFilePath, midasNodes, midasElements, plaques);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ошибка при обработке файлов. Excel: {excelFilePath}, ЛИРА: {liraSaprFilePath}", ex);
            }
        }
    }
}

