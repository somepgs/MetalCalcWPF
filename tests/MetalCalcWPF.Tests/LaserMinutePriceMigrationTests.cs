using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SQLite;
using MetalCalcWPF.Infrastructure.Migrations;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты миграции v3 — добавление полей «Цена минуты лазера» (воздух/кислород)
    /// в таблицу WorkshopSettings. Работаем с in-memory SQLite.
    /// </summary>
    [TestClass]
    public class LaserMinutePriceMigrationTests
    {
        [TestMethod]
        public void V003_OnFreshDatabase_ColumnsPresent_DefaultsApplied()
        {
            // Сценарий: свежая БД. V001 создаёт таблицу из актуальной модели
            // (уже с колонками LaserAirMinutePrice / LaserOxygenMinutePrice).
            // V003 должна увидеть, что колонки уже есть, и ничего не делать.
            using var db = new SQLiteConnection(":memory:");

            var applied = MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V003_AddLaserMinutePrices(),
            });

            CollectionAssert.AreEqual(new[] { 1, 3 }, applied);

            // Вставляем настройки «с нуля» — значения по умолчанию из модели (65 / 85).
            db.Insert(new WorkshopSettings());
            var settings = db.Table<WorkshopSettings>().First();

            Assert.AreEqual(65m, settings.LaserAirMinutePrice);
            Assert.AreEqual(85m, settings.LaserOxygenMinutePrice);
        }

        [TestMethod]
        public void V003_OnLegacyTable_AltersColumns_AndSetsDefaults()
        {
            // Сценарий: «старая» БД до Спринта 2.2c. Эмулируем её руками —
            // создаём таблицу WorkshopSettings без новых колонок и вставляем строку.
            using var db = new SQLiteConnection(":memory:");

            db.Execute(@"
                CREATE TABLE WorkshopSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElectricityPricePerKw decimal NOT NULL DEFAULT 0,
                    OperatorMonthlySalary decimal NOT NULL DEFAULT 500000
                )");
            db.Execute("INSERT INTO WorkshopSettings (ElectricityPricePerKw) VALUES (38)");

            // Применяем v3 напрямую — v1 трогать не нужно, мы уже симулировали её эффект.
            MigrationRunner.Run(db, new IMigration[] { new V003_AddLaserMinutePrices() });

            // Читаем сырыми SQL, чтобы не зависеть от sqlite-net mapping:
            var air = db.ExecuteScalar<decimal>("SELECT LaserAirMinutePrice FROM WorkshopSettings LIMIT 1");
            var oxy = db.ExecuteScalar<decimal>("SELECT LaserOxygenMinutePrice FROM WorkshopSettings LIMIT 1");

            Assert.AreEqual(65m, air, "Дефолт для воздуха должен быть 65 тг/мин (Excel B9)");
            Assert.AreEqual(85m, oxy, "Дефолт для кислорода должен быть 85 тг/мин (Excel B10)");
        }

        [TestMethod]
        public void V003_IsIdempotent_SecondRunDoesNotThrow()
        {
            using var db = new SQLiteConnection(":memory:");

            MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V003_AddLaserMinutePrices(),
            });

            // Повторный прогон не должен бросаться «duplicate column»
            MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V003_AddLaserMinutePrices(),
            });

            // Проверим, что колонки не задвоились: PRAGMA table_info вернёт их по одной штуке.
            var info = db.Query<PragmaCol>("PRAGMA table_info(\"WorkshopSettings\")");
            Assert.AreEqual(1, info.Count(c => c.name == "LaserAirMinutePrice"));
            Assert.AreEqual(1, info.Count(c => c.name == "LaserOxygenMinutePrice"));
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV003InOrder()
        {
            var all = MigrationRunner.GetAllMigrations();
            var versions = all.Select(m => m.Version).ToList();

            CollectionAssert.Contains(versions, 3, "Канонический список должен содержать V003");
            Assert.IsTrue(versions.IndexOf(2) < versions.IndexOf(3),
                "V002 должна идти до V003 в списке");
        }

        private class PragmaCol
        {
            public int cid { get; set; }
            public string name { get; set; } = string.Empty;
            public string type { get; set; } = string.Empty;
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }
    }
}
