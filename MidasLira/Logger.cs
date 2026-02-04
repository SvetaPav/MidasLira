﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidasLira
{
    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        CRITICAL
    }

    public class Logger
    {
        private static readonly object _lockObject = new object();
        private const string LOG_DIRECTORY = "Logs";
        private const int MAX_LOG_FILES = 30; // Хранить логи за 30 дней
        private readonly string _logFilePath;
        private readonly bool _enableConsoleOutput;

        public Logger(bool enableConsoleOutput = true)
        {
            _enableConsoleOutput = enableConsoleOutput;

            // Создаем директорию для логов
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LOG_DIRECTORY);
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Файл лога с текущей датой
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(logDirectory, $"app_{today}.log");

            // Очищаем старые логи
            CleanupOldLogs(logDirectory);

            // Записываем заголовок нового сеанса
            LogEvent(LogLevel.INFO, "=".PadRight(70, '='));
            LogEvent(LogLevel.INFO, $"Сеанс начат: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogEvent(LogLevel.INFO, $"Версия приложения: {GetAppVersion()}");
            LogEvent(LogLevel.INFO, "=".PadRight(70, '='));
        }

        /// <summary>
        /// Основной метод логирования
        /// </summary>
        public void LogEvent(LogLevel level, string message, Exception exception = null)
        {
            try
            {
                string logEntry = FormatLogEntry(level, message, exception);

                lock (_lockObject)
                {
                    // Запись в файл
                    using (StreamWriter writer = File.AppendText(_logFilePath))
                    {
                        writer.WriteLine(logEntry);
                    }
                }

                // Вывод в консоль (если включен)
                if (_enableConsoleOutput)
                {
                    Console.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                // Если не удалось записать в лог, выводим в консоль
                Console.WriteLine($"Ошибка при записи в лог: {ex.Message}");
                Console.WriteLine($"Сообщение для логирования: {message}");
            }
        }

        /// <summary>
        /// Форматирование записи лога
        /// </summary>
        private string FormatLogEntry(LogLevel level, string message, Exception exception)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = $"[{level.ToString().PadRight(8)}]";

            string logEntry = $"{timestamp} {levelStr} {message}";

            if (exception != null)
            {
                logEntry += $"\n{timestamp} {levelStr} Исключение: {exception.Message}";
                logEntry += $"\n{timestamp} {levelStr} StackTrace: {exception.StackTrace}";

                if (exception.InnerException != null)
                {
                    logEntry += $"\n{timestamp} {levelStr} Внутреннее исключение: {exception.InnerException.Message}";
                }
            }

            return logEntry;
        }

        /// <summary>
        /// Очистка старых лог-файлов
        /// </summary>
        private void CleanupOldLogs(string logDirectory)
        {
            try
            {
                var logFiles = Directory.GetFiles(logDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // Удаляем файлы старше MAX_LOG_FILES дней
                for (int i = MAX_LOG_FILES; i < logFiles.Count; i++)
                {
                    logFiles[i].Delete();
                    LogEvent(LogLevel.DEBUG, $"Удален старый лог-файл: {logFiles[i].Name}");
                }
            }
            catch (Exception ex)
            {
                LogEvent(LogLevel.WARNING, $"Не удалось очистить старые логи: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение версии приложения
        /// </summary>
        private string GetAppVersion()
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Логирование начала операции
        /// </summary>
        public void StartOperation(string operationName)
        {
            LogEvent(LogLevel.INFO, $"▶ НАЧАЛО: {operationName}");
        }

        /// <summary>
        /// Логирование завершения операции
        /// </summary>
        public void EndOperation(string operationName, bool success = true, TimeSpan? duration = null)
        {
            var status = success ? "УСПЕШНО" : "С ОШИБКОЙ";
            var durationStr = duration.HasValue ? $" (за {duration.Value.TotalSeconds:F2} сек.)" : "";
            LogEvent(LogLevel.INFO, $"◼ КОНЕЦ: {operationName} - {status}{durationStr}");
        }

        /// <summary>
        /// Логирование с параметрами (удобно для отладки)
        /// </summary>
        public void LogWithParameters(string message, Dictionary<string, object> parameters)
        {
            var paramStr = string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
            LogEvent(LogLevel.DEBUG, $"{message} | Параметры: {paramStr}");
        }

        /// <summary>
        /// Быстрые методы для разных уровней
        /// </summary>
        public void Debug(string message) => LogEvent(LogLevel.DEBUG, message);
        public void Info(string message) => LogEvent(LogLevel.INFO, message);
        public void Warning(string message) => LogEvent(LogLevel.WARNING, message);
        public void Error(string message, Exception ex = null) => LogEvent(LogLevel.ERROR, message, ex);
        public void Critical(string message, Exception ex = null) => LogEvent(LogLevel.CRITICAL, message, ex);

        /// <summary>
        /// Логирование выполнения блока кода с замером времени
        /// </summary>
        public T LogExecutionTime<T>(string operationName, Func<T> operation)
        {
            StartOperation(operationName);
            var startTime = DateTime.Now;

            try
            {
                var result = operation();
                EndOperation(operationName, true, DateTime.Now - startTime);
                return result;
            }
            catch (Exception ex)
            {
                EndOperation(operationName, false, DateTime.Now - startTime);
                Error($"Ошибка в операции '{operationName}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Для операций без возвращаемого значения
        /// </summary>
        public void LogExecutionTime(string operationName, Action operation)
        {
            StartOperation(operationName);
            var startTime = DateTime.Now;

            try
            {
                operation();
                EndOperation(operationName, true, DateTime.Now - startTime);
            }
            catch (Exception ex)
            {
                EndOperation(operationName, false, DateTime.Now - startTime);
                Error($"Ошибка в операции '{operationName}'", ex);
                throw;
            }
        }
    }
}