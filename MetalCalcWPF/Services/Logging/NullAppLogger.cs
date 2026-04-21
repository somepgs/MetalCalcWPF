using System;

namespace MetalCalcWPF.Services.Logging
{
    /// <summary>
    /// Логгер, который ничего не пишет. Используется:
    /// 1) в юнит-тестах — чтобы расчёты не плодили файлы в Documents;
    /// 2) как безопасный fallback, если по какой-то причине файловый логгер
    ///    не удалось проинициализировать (например, нет прав на запись).
    /// </summary>
    public class NullAppLogger : IAppLogger
    {
        public static readonly NullAppLogger Instance = new NullAppLogger();

        public void Info(string message, params object[] args) { }
        public void Warn(string message, params object[] args) { }
        public void Error(string message, params object[] args) { }
        public void Exception(Exception ex, string message, params object[] args) { }
    }
}
