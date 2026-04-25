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
    /// Тесты миграции v5 — справочники Workshop и Person + сид 5 внутренних цехов.
    /// </summary>
    [TestClass]
    public class WorkshopAndPersonMigrationTests
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
        public void V005_OnFreshDatabase_CreatesTables_AndSeeds5InternalWorkshops()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V005_AddWorkshopAndPerson(),
                });

                var workshops = ctx.Workshops.AsNoTracking().OrderBy(w => w.Id).ToList();
                Assert.AreEqual(5, workshops.Count, "Должно быть засеяно ровно 5 внутренних цехов");

                Assert.IsTrue(workshops.All(w => w.Kind == WorkshopKind.Internal));
                Assert.IsTrue(workshops.All(w => w.IsActive));

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "Цех СВ",
                        "Цех СК (столбы ЖБИ)",
                        "Цех ХЭСС (брусчатка и бордюры)",
                        "Цех сэндвич-панелей",
                        "Цех металлообработки",
                    },
                    workshops.Select(w => w.Name).ToArray());

                // Persons не сидируем — таблица существует, но пустая.
                Assert.AreEqual(0, ctx.Persons.AsNoTracking().Count());
            }
        }

        [TestMethod]
        public void V005_IsIdempotent_DoesNotDuplicateSeeds()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                // Первый прогон.
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V005_AddWorkshopAndPerson(),
                });
                var firstCount = ctx.Workshops.AsNoTracking().Count();

                // Полный заход во второй раз — V005 должна увидеть, что записи есть, и пропустить сид.
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V005_AddWorkshopAndPerson(),
                });

                Assert.AreEqual(firstCount, ctx.Workshops.AsNoTracking().Count(),
                    "Повторный прогон не должен дублировать сид цехов");
            }
        }

        [TestMethod]
        public void V005_OnLegacyDatabase_WithoutWorkshopTable_CreatesAndSeeds()
        {
            // Сценарий: БД, в которой Workshop ещё нет — например, до Этапа 2.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                // Эмулируем предыдущее состояние: только OrderHistory.
                ctx.Database.ExecuteSqlRaw(@"
                    CREATE TABLE OrderHistory (
                        Id integer primary key autoincrement not null,
                        CreatedDate bigint not null,
                        TotalPrice decimal not null
                    )");

                // Только V005 — она сама создаст таблицы через CREATE TABLE IF NOT EXISTS.
                MigrationRunner.Run(ctx, new IMigration[] { new V005_AddWorkshopAndPerson() });

                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table'")
                    .ToList();
                CollectionAssert.Contains(names, "Workshop");
                CollectionAssert.Contains(names, "Person");

                Assert.AreEqual(5, ctx.Workshops.AsNoTracking().Count());
            }
        }

        [TestMethod]
        public void V005_PersonWithWorkshopId_RoundtripsCorrectly()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V005_AddWorkshopAndPerson(),
                });

                var metalShop = ctx.Workshops.AsNoTracking().Single(w => w.Name == "Цех металлообработки");

                ctx.Persons.Add(new Person
                {
                    FullName = "Иванов И.И.",
                    Position = "Мастер",
                    WorkshopId = metalShop.Id,
                    CanSubmit = true,
                    CanAccept = true,
                    IsActive = true,
                    Notes = "Тест",
                });
                ctx.SaveChanges();

                var saved = ctx.Persons.AsNoTracking().Single();
                Assert.AreEqual("Иванов И.И.", saved.FullName);
                Assert.AreEqual(metalShop.Id, saved.WorkshopId);
                Assert.IsTrue(saved.CanSubmit);
                Assert.IsTrue(saved.CanAccept);
            }
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV005InOrder()
        {
            var all = MigrationRunner.GetAllMigrations();
            var versions = all.Select(m => m.Version).ToList();

            CollectionAssert.Contains(versions, 5, "Канонический список должен содержать V005");
            Assert.IsTrue(versions.IndexOf(4) < versions.IndexOf(5),
                "V004 должна идти до V005 в списке");
        }
    }
}
