using System;
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
    /// Тесты миграции v6 — расширение OrderHistory полями заявки
    /// (Priority, Quantity, MassKg, ApplicantName/WorkshopName/AcceptorName, MaterialName).
    /// </summary>
    [TestClass]
    public class OrderApplicationFieldsMigrationTests
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
        public void V006_OnFreshDatabase_ColumnsPresent_DefaultsApplied()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var applied = MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V006_AddOrderApplicationFields(),
                });

                CollectionAssert.AreEqual(new[] { 1, 6 }, applied);

                ctx.OrderHistory.Add(new OrderHistory
                {
                    CreatedDate = DateTime.UtcNow,
                    ClientName = "Тест",
                    Description = "x",
                    OperationType = "Металл",
                    TotalPrice = 1000m,
                });
                ctx.SaveChanges();

                var saved = ctx.OrderHistory.AsNoTracking().First();
                Assert.AreEqual(OrderPriority.Normal, saved.Priority,
                    "DEFAULT 1 в DDL соответствует OrderPriority.Normal");
                Assert.AreEqual(0, saved.Quantity);
                Assert.AreEqual(0d, saved.MassKg);
                Assert.IsNull(saved.ApplicantName);
                Assert.IsNull(saved.ApplicantWorkshopName);
                Assert.IsNull(saved.AcceptorName);
                Assert.IsNull(saved.MaterialName);
            }
        }

        [TestMethod]
        public void V006_OnLegacyTable_AltersColumns_PreservesExistingRows()
        {
            // Эмулируем БД до миграции v6 — OrderHistory с колонками v4,
            // но без полей заявки. Существующий заказ должен сохраниться.
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
                        OperationType varchar,
                        MaterialCost decimal not null default 0,
                        LaserCost decimal not null default 0,
                        BendingCost decimal not null default 0,
                        WeldingCost decimal not null default 0
                    )");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO OrderHistory (CreatedDate, ClientName, Description, TotalPrice, OperationType, MaterialCost) " +
                    "VALUES (123456789, 'Старый клиент', 'x', 50000, 'Лазер', 10000)");

                // V008 тоже прогоняем — модель уже знает CompletedDate, без него
                // EF-чтение строки в конце теста упадёт на «no such column».
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V006_AddOrderApplicationFields(),
                    new V008_AddOrderCompletedDate(),
                });

                // Все 7 колонок добавлены.
                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"OrderHistory\")")
                    .ToList();
                CollectionAssert.Contains(names, "Priority");
                CollectionAssert.Contains(names, "Quantity");
                CollectionAssert.Contains(names, "MassKg");
                CollectionAssert.Contains(names, "ApplicantName");
                CollectionAssert.Contains(names, "ApplicantWorkshopName");
                CollectionAssert.Contains(names, "AcceptorName");
                CollectionAssert.Contains(names, "MaterialName");

                // Старая строка целая.
                var legacy = ctx.OrderHistory.AsNoTracking().First();
                Assert.AreEqual("Старый клиент", legacy.ClientName);
                Assert.AreEqual(50000m, legacy.TotalPrice);
                Assert.AreEqual(10000m, legacy.MaterialCost);
                Assert.AreEqual(OrderPriority.Normal, legacy.Priority,
                    "Default 1 → Normal, чтобы исторический заказ не отображался как Низкая");
                Assert.AreEqual(0, legacy.Quantity);
                Assert.AreEqual(0d, legacy.MassKg);
                Assert.IsNull(legacy.ApplicantName);
            }
        }

        [TestMethod]
        public void V006_NewOrderRoundtripsAllFields()
        {
            // Сохраняем заказ со всеми новыми полями — проверяем, что они корректно
            // читаются обратно. Это страховка от ошибок в маппинге enum/decimal/double.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V006_AddOrderApplicationFields(),
                });

                ctx.OrderHistory.Add(new OrderHistory
                {
                    CreatedDate = new DateTime(2025, 4, 5, 10, 30, 0, DateTimeKind.Local),
                    ClientName = "К",
                    Description = "Деталь",
                    OperationType = "Металл + Лазер",
                    TotalPrice = 250000m,
                    MaterialCost = 80000m,
                    LaserCost = 170000m,
                    Priority = OrderPriority.Urgent,
                    Quantity = 12,
                    MassKg = 47.3,
                    ApplicantName = "Иванов И.И.",
                    ApplicantWorkshopName = "Цех СВ",
                    AcceptorName = "Петров П.П.",
                    MaterialName = "Сталь Ст3",
                });
                ctx.SaveChanges();

                var saved = ctx.OrderHistory.AsNoTracking().First();
                Assert.AreEqual(OrderPriority.Urgent, saved.Priority);
                Assert.AreEqual(12, saved.Quantity);
                Assert.AreEqual(47.3, saved.MassKg, 0.001);
                Assert.AreEqual("Иванов И.И.", saved.ApplicantName);
                Assert.AreEqual("Цех СВ", saved.ApplicantWorkshopName);
                Assert.AreEqual("Петров П.П.", saved.AcceptorName);
                Assert.AreEqual("Сталь Ст3", saved.MaterialName);
            }
        }

        [TestMethod]
        public void V006_IsIdempotent_SecondRunDoesNotThrow()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V006_AddOrderApplicationFields(),
                });

                // Повтор не должен бросать «duplicate column».
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V006_AddOrderApplicationFields(),
                });

                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"OrderHistory\")")
                    .ToList();
                Assert.AreEqual(1, names.Count(n => n == "Priority"));
                Assert.AreEqual(1, names.Count(n => n == "ApplicantName"));
            }
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV006InOrder()
        {
            var all = MigrationRunner.GetAllMigrations();
            var versions = all.Select(m => m.Version).ToList();

            CollectionAssert.Contains(versions, 6);
            Assert.IsTrue(versions.IndexOf(5) < versions.IndexOf(6),
                "V005 должна идти до V006");
        }
    }
}
