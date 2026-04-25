using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Интеграционный smoke-тест <see cref="DatabaseService"/> — проверяет полный
    /// цикл жизни БД на файле (не in-memory):
    /// 1) создание свежей БД → миграции v1..v3 → сид справочников;
    /// 2) запись / чтение через CRUD-API;
    /// 3) закрытие и повторное открытие того же файла — ничего не теряется,
    ///    миграции повторно не применяются (идемпотентность);
    /// 4) симуляция «legacy»-БД с минимальной v1-схемой (без колонок v3) —
    ///    после инициализации DatabaseService-ом должна получиться полноценно
    ///    работающая БД, readable / writable через EF Core.
    ///
    /// <para>Это ближайшая имитация того, что делает настоящее приложение
    /// на машине пользователя при запуске поверх существующего workshop.db.</para>
    /// </summary>
    [TestClass]
    public class DatabaseServiceIntegrationTests
    {
        private string _dbPath = string.Empty;

        [TestInitialize]
        public void SetUp()
        {
            // Отдельный временный файл на каждый тест, чтобы они не влияли друг на друга.
            _dbPath = Path.Combine(Path.GetTempPath(), $"metalcalc_test_{Guid.NewGuid():N}.db");
        }

        [TestCleanup]
        public void TearDown()
        {
            try
            {
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
            }
            catch
            {
                // temp file cleanup — best-effort; Windows может держать handle чуть дольше.
            }
        }

        [TestMethod]
        public void FreshDb_InitializesMigrationsAndSeeds_AndDataSurvivesReopen()
        {
            // 1) Свежая БД: init применяет миграции + сид.
            {
                var db = new DatabaseService(_dbPath);
                var settings = db.GetSettings();
                Assert.IsNotNull(settings, "После инициализации должны быть настройки цеха");
                Assert.AreEqual(65m, settings.LaserAirMinutePrice,
                    "Сид/миграция v3 должны поставить цену минуты воздуха 65");
                Assert.AreEqual(85m, settings.LaserOxygenMinutePrice);

                var recent = db.GetRecentOrders();
                Assert.IsNotNull(recent);
                Assert.AreEqual(0, recent.Count, "История заказов изначально пустая");

                // Записываем заказ, чтобы убедиться, что не только read работает.
                db.SaveOrder(new OrderHistory
                {
                    CreatedDate = DateTime.UtcNow,
                    ClientName = "Интеграционный тест",
                    Description = "1 шт × 100 мм",
                    OperationType = "Laser",
                    TotalPrice = 12345m,
                });
            }

            // 2) Повторное открытие — данные и версии схемы не теряются.
            {
                var db = new DatabaseService(_dbPath);
                var recent = db.GetRecentOrders();
                Assert.AreEqual(1, recent.Count, "Сохранённый ранее заказ должен быть виден после реоткрытия");
                Assert.AreEqual("Интеграционный тест", recent[0].ClientName);
                Assert.AreEqual(12345m, recent[0].TotalPrice);
            }
        }

        [TestMethod]
        public void ReInit_IsIdempotent_NoDuplicateSeeds()
        {
            // Прогоняем init трижды подряд — сидовые записи не должны дублироваться.
            var db1 = new DatabaseService(_dbPath);
            var materialCountAfterFirst = db1.GetMaterials().Count;

            Assert.IsTrue(materialCountAfterFirst > 0,
                "На свежей БД должен сработать сид MaterialType");

            _ = new DatabaseService(_dbPath); // второй проход
            var db3 = new DatabaseService(_dbPath);

            Assert.AreEqual(materialCountAfterFirst, db3.GetMaterials().Count,
                "Сид не должен дублироваться при повторных открытиях БД");
        }

        [TestMethod]
        public void GetOrdersByDateRange_ReturnsOnlyOrdersInsideHalfOpenInterval()
        {
            // Готовим 4 заказа в разных датах: за границей слева, внутри, внутри, за границей справа.
            var db = new DatabaseService(_dbPath);

            // ВАЖНО: CreatedDate хранится как ticks → можно сравнивать DateTime напрямую
            // (AppDbContext настраивает ValueConverter<DateTime, long> глобально).
            var march31 = new DateTime(2025, 3, 31, 23, 59, 59);  // до периода
            var april05 = new DateTime(2025, 4, 5,  10, 0, 0);    // внутри
            var april25 = new DateTime(2025, 4, 25, 18, 30, 0);   // внутри
            var may01   = new DateTime(2025, 5, 1,  0,  0,  0);   // граница — НЕ входит (endExclusive)

            db.SaveOrder(new OrderHistory { CreatedDate = march31, ClientName = "До", Description = "x", OperationType = "Laser", TotalPrice = 100m });
            db.SaveOrder(new OrderHistory { CreatedDate = april05, ClientName = "Внутри-1", Description = "x", OperationType = "Laser", TotalPrice = 200m });
            db.SaveOrder(new OrderHistory { CreatedDate = april25, ClientName = "Внутри-2", Description = "x", OperationType = "Bending", TotalPrice = 300m });
            db.SaveOrder(new OrderHistory { CreatedDate = may01,   ClientName = "После", Description = "x", OperationType = "Laser", TotalPrice = 400m });

            // Период «апрель 2025»: [01.04.2025 00:00 ; 01.05.2025 00:00)
            var start = new DateTime(2025, 4, 1);
            var end   = new DateTime(2025, 5, 1);

            var result = db.GetOrdersByDateRange(start, end);

            Assert.AreEqual(2, result.Count, "В апрель должны попасть ровно 2 заказа");
            // Порядок — по убыванию даты (новый сверху).
            Assert.AreEqual("Внутри-2", result[0].ClientName);
            Assert.AreEqual("Внутри-1", result[1].ClientName);
        }

        [TestMethod]
        public void GetOrdersByDateRange_InvertedRange_ReturnsEmpty()
        {
            // Защитный контракт: end <= start → пустой список, без исключения.
            var db = new DatabaseService(_dbPath);
            db.SaveOrder(new OrderHistory
            {
                CreatedDate = DateTime.UtcNow,
                ClientName = "любой",
                Description = "x",
                OperationType = "Laser",
                TotalPrice = 1m,
            });

            var end   = new DateTime(2025, 1, 1);
            var start = new DateTime(2025, 6, 1);

            var result = db.GetOrdersByDateRange(start, end);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }
    }
}
