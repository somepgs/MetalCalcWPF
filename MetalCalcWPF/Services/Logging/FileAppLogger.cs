using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MetalCalcWPF.Services.Logging
{
    /// <summary>
    /// Файловый логгер. Пишет в папку <c>logs/</c> внутри указанной директории,
    /// по одному файлу на сутки (<c>YYYY-MM-DD.log</c>).
    ///
    /// Формат строки:
    /// <code>2026-04-21 14:33:07.123 [INFO ] Заказ рассчитан: 45 000 тг</code>
    ///
    /// Thread-safe: все записи сериализуются через <c>lock</c>. Для настольного
    /// приложения с одиночным пользователем этого достаточно и проще, чем
    /// отдельный поток/очередь.
    ///
    /// Ошибки записи НЕ пробрасываются — если журнал не удалось записать,
    /// приложение не должно падать. Логгер — инструмент диагностики, а не
    /// критическая подсистема.
    /// </summary>
    public class FileAppLogger : IAppLogger
    {
        private readonly string _logsFolder;
        private readonly object _lock = new object();

        /// <summary>
        /// Создаёт логгер, пишущий в <paramref name="logsFolder"/>.
        /// Папка создаётся, если её ещё нет.
        /// </summary>
        /// <param name="logsFolder">Полный путь к папке логов (не к файлу).</param>
        public FileAppLogger(string logsFolder)
        {
            if (string.IsNullOrWhiteSpace(logsFolder))
                throw new ArgumentException("Путь к папке логов не должен быть пустым", nameof(logsFolder));

            _logsFolder = logsFolder;
            try
            {
                if (!Directory.Exists(_logsFolder))
                    Directory.CreateDirectory(_logsFolder);
            }
            catch
            {
                // Не роняем приложение, если папку создать не удалось.
                // Последующие записи тихо провалятся.
            }
        }

        /// <summary>
        /// Фабрика: логгер в стандартном месте
        /// <c>%MyDocuments%\MetalCalc\logs\</c>.
        /// </summary>
        public static FileAppLogger CreateDefault()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var folder = Path.Combine(docs, "MetalCalc", "logs");
            return new FileAppLogger(folder);
        }

        public void Info(string message, params object[] args)  => Write("INFO ", message, args);
        public void Warn(string message, params object[] args)  => Write("WARN ", message, args);
        public void Error(string message, params object[] args) => Write("ERROR", message, args);

        public void Exception(Exception ex, string message, params object[] args)
        {
            var formatted = SafeFormat(message, args);
            var full = $"{formatted}{Environment.NewLine}  {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            Write("ERROR", full, Array.Empty<object>());
        }

        /// <summary>
        /// Полный путь к файлу лога на заданную дату — публично ради тестов.
        /// </summary>
        public string GetLogFilePathFor(DateTime utcDate)
        {
            var name = utcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";
            return Path.Combine(_logsFolder, name);
        }

        private void Write(string level, string message, object[] args)
        {
            var now = DateTime.Now;
            var formatted = SafeFormat(message, args);
            var line = new StringBuilder()
                .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" [").Append(level).Append("] ")
                .Append(formatted)
                .Append(Environment.NewLine)
                .ToString();

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(GetLogFilePathFor(now), line, Encoding.UTF8);
                }
                catch
                {
                    // Молча игнорируем — логгер не должен ронять приложение.
                }
            }
        }

        private static string SafeFormat(string message, object[] args)
        {
            if (args == null || args.Length == 0) return message ?? string.Empty;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, message ?? string.Empty, args);
            }
            catch (FormatException)
            {
                // Плохой шаблон — не теряем информацию, пишем как есть.
                return (message ?? string.Empty) + " [args: " + string.Join(", ", args) + "]";
            }
        }
    }
}
