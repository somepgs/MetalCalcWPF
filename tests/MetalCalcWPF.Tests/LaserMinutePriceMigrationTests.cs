using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF.Infrastructure.Migrations;
using MetalCalcWPF.Infrastructure.Persistence;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты миграции v3 — добавление полей «Цена минуты лазера» (воздух/кислород)
    /// в таблицу WorkshopSettings. Работаем с in-memory SQLite через <see cref="AppDbContext"/>.
    /// </summary>
    [TestClass]
    public class LaserMinutePriceMigrationTests
    {
        private static (SqliteConnection conn, AppDbContext ctx) CreateInMemory()
        {
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(conn)
                .Options;
            return (conn, new AppDbContext(options));
        }

        [TestMethod]
        public void V003_OnFreshDatabase_ColumnsPresent_DefaultsApplied()
        {
            // Сценарий: свежая БД. V001 создаёт таблицу из актуальной DDL
            // (уже с колонками LaserAirMinutePrice / LaserOxygenMinutePrice).
            // V003 должна увидеть, что колонки уже есть, и ничего не делать.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var applied = MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V003_AddLaserMinutePrices(),
                });

                CollectionAssert.AreEqual(new[] { 1, 3 }, applied);

                // Вставляем настройки «с нуля» — значения по умолчанию из модели (65 / 85).
                ctx.WorkshopSettings.Add(new WorkshopSettings());
                ctx.SaveChanges();

                var settings = ctx.WorkshopSettings.AsNoTracking().First();

                Assert.AreEqual(65m, settings.LaserAirMinutePrice);
                Assert.AreEqual(85m, settings.LaserOxygenMinutePrice);
            }
        }

        [TestMethod]
        public void V003_OnLegacyTable_AltersColumns_AndSetsDefaults()
        {
            // Сценарий: «старая» БД до Спринта 2.2c. Эмулируем её руками —
            // создаём таблицу WorkshopSettings без новых колонок и вставляем строку.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                ctx.Database.ExecuteSqlRaw(@"
                    CREATE TABLE WorkshopSettings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ElectricityPricePerKw decimal NOT NULL DEFAULT 0,
                        OperatorMonthlySalary decimal NOT NULL DEFAULT 500000
                    )");
                ctx.Database.ExecuteSqlRaw("INSERT INTO WorkshopSettings (ElectricityPricePerKw) VALUES (38)");

                // Применяем v3 напрямую — v1 трогать не нужно, мы уже симулировали её эффект.
                MigrationRunner.Run(ctx, new IMigration[] { new V003_AddLaserMinutePrices() });

                // Читаем сырыми SQL, чтобы не зависеть от EF-маппинга:
                var air = ctx.Database
                    .SqlQueryRaw<decimal>("SELECT LaserAirMinutePrice AS Value FROM WorkshopSettings LIMIT 1")
                    .AsEnumerable()
                    .First();
                var oxy = ctx.Database
                    .SqlQueryRaw<decimal>("SELECT LaserOxygenMinutePrice AS Value FROM WorkshopSettings LIMIT 1")
                    .AsEnumerable()
                    .First();

                Assert.AreEqual(65m, air, "Дефолт для воздуха должен быть 65 тг/мин (Excel B9)");
                Assert.AreEqual(85m, oxy, "Дефолт для кислорода должен быть 85 тг/мин (Excel B10)");
            }
        }

        [TestMethod]
        public void V003_IsIdempotent_SecondRunDoesNotThrow()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V003_AddLaserMinutePrices(),
                });

                // Повторный прогон не должен бросаться «duplicate column».
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V003_AddLaserMinutePrices(),
                });

                // Проверим, что колонки не задвоились: pragma_table_info вернёт их по одной штуке.
                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"WorkshopSettings\")")
                    .ToList();
                Assert.AreEqual(1, names.Count(n => n == "LaserAirMinutePrice"));
                Assert.AreEqual(1, names.Count(n => n == "LaserOxygenMinutePrice"));
            }
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
    }
}
