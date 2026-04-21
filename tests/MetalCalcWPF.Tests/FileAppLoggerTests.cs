using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF.Services.Logging;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты файлового логгера. Работают в уникальной временной папке,
    /// которую создаёт TestInitialize и удаляет TestCleanup — никаких следов
    /// в Documents\MetalCalc.
    /// </summary>
    [TestClass]
    public class FileAppLoggerTests
    {
        private string _folder = string.Empty;

        [TestInitialize]
        public void Init()
        {
            _folder = Path.Combine(Path.GetTempPath(), "MetalCalcLoggerTests_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
            catch { /* не критично */ }
        }

        [TestMethod]
        public void Info_CreatesLogFileAndWritesLine()
        {
            var log = new FileAppLogger(_folder);
            log.Info("Hello {0}", "world");

            var path = log.GetLogFilePathFor(DateTime.Now);
            Assert.IsTrue(File.Exists(path), "Файл лога должен быть создан при первой записи");

            var content = File.ReadAllText(path);
            StringAssert.Contains(content, "[INFO ]");
            StringAssert.Contains(content, "Hello world");
        }

        [TestMethod]
        public void Warn_Error_UseCorrectLevels()
        {
            var log = new FileAppLogger(_folder);
            log.Warn("w-msg");
            log.Error("e-msg");

            var content = File.ReadAllText(log.GetLogFilePathFor(DateTime.Now));
            StringAssert.Contains(content, "[WARN ] w-msg");
            StringAssert.Contains(content, "[ERROR] e-msg");
        }

        [TestMethod]
        public void Exception_IncludesMessageAndStackTrace()
        {
            var log = new FileAppLogger(_folder);
            Exception caught;
            try { throw new InvalidOperationException("test-boom"); }
            catch (Exception ex) { caught = ex; }

            log.Exception(caught, "Падение в {0}", "подсистеме X");

            var content = File.ReadAllText(log.GetLogFilePathFor(DateTime.Now));
            StringAssert.Contains(content, "[ERROR]");
            StringAssert.Contains(content, "Падение в подсистеме X");
            StringAssert.Contains(content, "InvalidOperationException");
            StringAssert.Contains(content, "test-boom");
        }

        [TestMethod]
        public void LogFilePath_IsDatedYyyyMmDd()
        {
            var log = new FileAppLogger(_folder);
            var path = log.GetLogFilePathFor(new DateTime(2026, 4, 21));
            Assert.AreEqual("2026-04-21.log", Path.GetFileName(path));
        }

        [TestMethod]
        public void MissingFolder_IsCreatedOnConstruct()
        {
            var nested = Path.Combine(_folder, "sub", "deeper");
            var log = new FileAppLogger(nested);

            Assert.IsTrue(Directory.Exists(nested), "Папка лога должна создаться автоматически");
            log.Info("ping");
            Assert.IsTrue(File.Exists(log.GetLogFilePathFor(DateTime.Now)));
        }

        [TestMethod]
        public void BadFormatString_DoesNotThrowAndKeepsMessage()
        {
            var log = new FileAppLogger(_folder);
            // {0} есть, но ни одного args — string.Format бросил бы FormatException
            log.Info("bad {0} template");

            var content = File.ReadAllText(log.GetLogFilePathFor(DateTime.Now));
            StringAssert.Contains(content, "bad {0} template");
        }

        [TestMethod]
        public void ConcurrentWrites_DoNotLoseLines()
        {
            var log = new FileAppLogger(_folder);

            // 4 потока × 50 строк = 200 строк. Без lock-а часть теряется на Windows.
            Parallel.For(0, 4, t =>
            {
                for (int i = 0; i < 50; i++)
                    log.Info("t{0}-{1}", t, i);
            });

            var content = File.ReadAllText(log.GetLogFilePathFor(DateTime.Now));
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(200, lines.Length,
                "Все 200 строк должны попасть в файл — лог должен быть thread-safe");
        }

        [TestMethod]
        public void NullAppLogger_DoesNothingAndDoesNotThrow()
        {
            // Контрактная проверка — чтобы убедиться, что fallback-логгер безопасен.
            var log = NullAppLogger.Instance;
            log.Info("x");
            log.Warn("x");
            log.Error("x");
            log.Exception(new Exception("e"), "m");
            // Если дошли сюда без исключений — успех.
        }
    }
}
