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
        private const string LOG_FILE_NAME = "application.log";

        /// <summary>
        /// Регистрирует событие в лог-файл.
        /// </summary>
        public void LogEvent(string eventType, string message)
        {
            try
            {
                // Формируем строку для записи в лог
                string logEntry = $"{DateTime.Now}: [{eventType}] {message}";

                // Добавляем запись в лог-файл
                using (StreamWriter streamWriter = File.AppendText(LOG_FILE_NAME))
                {
                    streamWriter.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                // Если произошла ошибка при записи лога, выводим сообщение в консоль
                Console.WriteLine($"Ошибка при записи в лог: {ex.Message}");
            }
        }
    }
}
