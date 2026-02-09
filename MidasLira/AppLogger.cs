using System;

namespace MidasLira
{
    public static class AppLogger
    {
        private static Logger? _instance;
        private static readonly object _lock = new();

        public static bool IsInitialized => _instance != null;

        public static void Initialize(Logger logger)
        {
            lock (_lock)
            {
                _instance = logger ?? throw new ArgumentNullException(nameof(logger));
                Info("AppLogger инициализирован");
            }
        }

        public static void Debug(string message) => _instance?.Debug(message);
        public static void Info(string message) => _instance?.Info(message);
        public static void Warning(string message) => _instance?.Warning(message);
        public static void Error(string message, Exception? ex = null) => _instance?.Error(message, ex);

        // Метод для получения пути к файлу логов (для пользователя)
        public static string GetLogFilePath()
        {
            return _instance?.LogFilePath() ?? string.Empty;
        }
    }
}