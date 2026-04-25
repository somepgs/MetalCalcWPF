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
    /// Тесты миграции v4 — добавление 4 cost-колонок в таблицу OrderHistory
    /// (MaterialCost / LaserCost / BendingCost / WeldingCost) для отчётности руководству.
    /// </summary>
    [TestClass]
    public class OrderCostBreakdownMigrationTests
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
        public void V004_OnFreshDatabase_ColumnsPresent_DefaultsApplied()
        {
            // V001 актуальной редакции уже создаёт OrderHistory сразу с 4 cost-колонками.
            // V004 видит, что они на месте, ALTER TABLE не выполняет.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var applied = MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V004_AddOrderCostBreakdown(),
                });

                CollectionAssert.AreEqual(new[] { 1, 4 }, applied);

                // Вставляем заказ без явных cost-полей — должны проставиться 0 (DEFAULT 0).
                ctx.OrderHistory.Add(new OrderHistory
                {
                    CreatedDate = System.DateTime.UtcNow,
                    ClientName = "Тест",
                    Description = "x",
                    OperationType = "Металл + Лазер",
                    TotalPrice = 12345m,
                });
                ctx.SaveChanges();

                var saved = ctx.OrderHistory.AsNoTracking().First();
                Assert.AreEqual(0m, saved.MaterialCost);
                Assert.AreEqual(0m, saved.LaserCost);
                Assert.AreEqual(0m, saved.BendingCost);
                Assert.AreEqual(0m, saved.WeldingCost);
            }
        }

        [TestMethod]
        public void V004_OnLegacyTable_AltersColumns_PreservesExistingRows()
        {
            // Эмулируем БД до миграции v4: OrderHistory с 6 колонками.
            // После v4 у старых строк cost-поля = 0 (DEFAULT), TotalPrice не теряется.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                ctx.Database.ExecuteSqlRaw(@"
                    CREATE TABLE OrderHistory (
                        Id integer primary key autoincrement not null,
                        CreatedDate bigint not null,
                        ClientName varchar,
                        Description varchar,
                        TotalPrice decimal not null,
                        OperationType varchar
                    )");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO OrderHistory (CreatedDate, ClientName, Description, TotalPrice, OperationType) " +
                    "VALUES (123456789, 'Старый клиент', 'Историческая запись', 99999, 'Laser')");

                MigrationRunner.Run(ctx, new IMigration[] { new V004_AddOrderCostBreakdown() });

                // Колонки появились.
                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"OrderHistory\")")
                    .ToList();
                CollectionAssert.Contains(names, "MaterialCost");
                CollectionAssert.Contains(names, "LaserCost");
                CollectionAssert.Contains(names, "BendingCost");
                CollectionAssert.Contains(names, "WeldingCost");

                // Старая строка — TotalPrice сохранился, cost-поля = 0.
                var legacy = ctx.OrderHistory.AsNoTracking().First();
                Assert.AreEqual("Старый клиент", legacy.ClientName);
                Assert.AreEqual(99999m, legacy.TotalPrice);
                Assert.AreEqual(0m, legacy.MaterialCost);
                Assert.AreEqual(0m, legacy.LaserCost);
                Assert.AreEqual(0m, legacy.BendingCost);
                Assert.AreEqual(0m, legacy.WeldingCost);
            }
        }

        [TestMethod]
        public void V004_IsIdempotent_SecondRunDoesNotThrow()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V004_AddOrderCostBreakdown(),
                });

                // Повторный прогон не должен бросаться «duplicate column».
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V004_AddOrderCostBreakdown(),
                });

                // Колонки не задвоились.
                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"OrderHistory\")")
                    .ToList();
                Assert.AreEqual(1, names.Count(n => n == "MaterialCost"));
                Assert.AreEqual(1, names.Count(n => n == "LaserCost"));
                Assert.AreEqual(1, names.Count(n => n == "BendingCost"));
                Assert.AreEqual(1, names.Count(n => n == "WeldingCost"));
            }
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV004InOrder()
        {
            var all = MigrationRunner.GetAllMigrations();
            var versions = all.Select(m => m.Version).ToList();

            CollectionAssert.Contains(versions, 4, "Канонический список должен содержать V004");
            Assert.IsTrue(versions.IndexOf(3) < versions.IndexOf(4),
                "V003 должна идти до V004 в списке");
        }
    }
}
