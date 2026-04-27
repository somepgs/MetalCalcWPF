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
    /// Тесты миграции v8 — добавление CompletedDate в OrderHistory для статуса
    /// «выполнен» (Этап 4).
    /// </summary>
    [TestClass]
    public class OrderCompletedDateMigrationTests
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
        public void V008_OnFreshDatabase_ColumnPresent_DefaultsToNull()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V008_AddOrderCompletedDate(),
                });

                ctx.OrderHistory.Add(new OrderHistory
                {
                    CreatedDate = DateTime.UtcNow,
                    ClientName = "К",
                    Description = "x",
                    OperationType = "Металл",
                    TotalPrice = 1000m,
                });
                ctx.SaveChanges();

                var saved = ctx.OrderHistory.AsNoTracking().First();
                Assert.IsNull(saved.CompletedDate, "По умолчанию заказ не отмечен выполненным");
            }
        }

        [TestMethod]
        public void V008_RoundtripsCompletedDate()
        {
            // Записываем заказ с заполненной датой выполнения и проверяем, что
            // EF корректно сериализует её в bigint ticks и читает обратно.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V008_AddOrderCompletedDate(),
                });

                var completedAt = new DateTime(2025, 4, 15, 14, 30, 0, DateTimeKind.Local);
                ctx.OrderHistory.Add(new OrderHistory
                {
                    CreatedDate = new DateTime(2025, 4, 10),
                    ClientName = "К", Description = "x", OperationType = "Лазер",
                    TotalPrice = 5000m,
                    CompletedDate = completedAt,
                });
                ctx.SaveChanges();

                var saved = ctx.OrderHistory.AsNoTracking().First();
                Assert.IsNotNull(saved.CompletedDate);
                Assert.AreEqual(completedAt, saved.CompletedDate);
            }
        }

        [TestMethod]
        public void V008_OnLegacyTable_AltersColumn_PreservesRows()
        {
            // Эмулируем БД до v8 — OrderHistory с полями v6, без CompletedDate.
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
                        WeldingCost decimal not null default 0,
                        Priority integer not null default 1,
                        Quantity integer not null default 0,
                        MassKg float not null default 0,
                        ApplicantName varchar,
                        ApplicantWorkshopName varchar,
                        AcceptorName varchar,
                        MaterialName varchar
                    )");
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO OrderHistory (CreatedDate, ClientName, TotalPrice, OperationType) " +
                    "VALUES (123456789, 'Старый', 99999, 'Лазер')");

                MigrationRunner.Run(ctx, new IMigration[] { new V008_AddOrderCompletedDate() });

                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"OrderHistory\")")
                    .ToList();
                CollectionAssert.Contains(names, "CompletedDate");

                var legacy = ctx.OrderHistory.AsNoTracking().First();
                Assert.AreEqual("Старый", legacy.ClientName);
                Assert.AreEqual(99999m, legacy.TotalPrice);
                Assert.IsNull(legacy.CompletedDate, "Legacy-заказ должен прийти как «не выполнен»");
            }
        }

        [TestMethod]
        public void V008_IsIdempotent_SecondRunDoesNotThrow()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V008_AddOrderCompletedDate(),
                });

                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V008_AddOrderCompletedDate(),
                });

                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"OrderHistory\")")
                    .ToList();
                Assert.AreEqual(1, names.Count(n => n == "CompletedDate"));
            }
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV008InOrder()
        {
            var versions = MigrationRunner.GetAllMigrations().Select(m => m.Version).ToList();
            CollectionAssert.Contains(versions, 8);
            Assert.IsTrue(versions.IndexOf(7) < versions.IndexOf(8));
        }
    }
}
