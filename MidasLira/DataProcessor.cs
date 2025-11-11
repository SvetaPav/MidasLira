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
            _rigidityCalculator = rigidityCalculator;
            _writer = writer;
            _excelReader = excelReader;
        }

        /// <summary>
        /// Главный метод обработки данных.
        /// </summary>
        public bool ProcessFile(string excelFilePath, string liraSaprFilePath)
        {
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
                throw new Exception("Ошибка при обработке данных.", ex);
            }
        }
    }
}

