using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public class Logger
    {
        private const string LOG_FILE_PATH = "app_log.txt";

        /// <summary>
        /// Записывает событие в лог-файл.
        /// </summary>
        public void LogEvent(string level, string message)
        {
            try
            {
                // Формат строки лога: ДатаВремя Уровень Сообщение
                string logEntry = $"{DateTime.Now}: [{level}] {message}";

                // Добавляем запись в конец файла
                using (StreamWriter writer = File.AppendText(LOG_FILE_PATH))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки при записи в лог выводим информацию в консоль
                Console.WriteLine($"Ошибка при записи в лог: {ex.Message}");
            }
        }
    }
}
