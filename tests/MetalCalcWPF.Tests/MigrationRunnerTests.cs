using System;
using System.Collections.Generic;
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
    /// Тесты раннера миграций. Все тесты работают с in-memory SQLite через
    /// живое <see cref="SqliteConnection"/>, которое передаётся в <see cref="AppDbContext"/>
    /// — файл на диске не создаётся, состояние изолировано по тесту.
    ///
    /// <para>Ключевой приём: открытое Sqlite-соединение <c>Data Source=:memory:</c>
    /// живёт ровно столько, сколько живёт тест, и один и тот же контекст может
    /// делать несколько раундов миграций на одной и той же «базе».</para>
    /// </summary>
    [TestClass]
    public class MigrationRunnerTests
    {
        // --- Вспомогательные миграции для тестов ---

        private class FakeV1 : IMigration
        {
            public int Version => 1;
            public string Description => "fake v1";
            public int UpCallCount;
            public void Up(AppDbContext ctx)
            {
                UpCallCount++;
                ctx.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS T1 (Id INTEGER PRIMARY KEY)");
            }
        }

        private class FakeV2 : IMigration
        {
            public int Version => 2;
            public string Description => "fake v2";
            public int UpCallCount;
            public void Up(AppDbContext ctx)
            {
                UpCallCount++;
                ctx.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS T2 (Id INTEGER PRIMARY KEY)");
            }
        }

        private class FailingMigration : IMigration
        {
            public int Version => 2;
            public string Description => "will throw";
            public void Up(AppDbContext ctx)
            {
                // Создадим таблицу, потом сорвёмся — транзакция должна откатить.
                ctx.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS TFail (Id INTEGER PRIMARY KEY)");
                throw new InvalidOperationException("boom");
            }
        }

        private class BadVersionMigration : IMigration
        {
            public int Version => 0;
            public string Description => "bad";
            public void Up(AppDbContext ctx) { }
        }

        // --- Инфра для in-memory EF Core ---

        private static (SqliteConnection conn, AppDbContext ctx) CreateInMemory()
        {
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(conn)
                .Options;
            return (conn, new AppDbContext(options));
        }

        // --- Тесты ---

        [TestMethod]
        public void Run_FreshDatabase_AppliesV1AndRecordsIt()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var v1 = new FakeV1();

                var applied = MigrationRunner.Run(ctx, new IMigration[] { v1 });

                Assert.AreEqual(1, v1.UpCallCount, "Up должен быть вызван ровно один раз");
                CollectionAssert.AreEqual(new[] { 1 }, applied);

                var rows = ctx.SchemaVersions.AsNoTracking().ToList();
                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(1, rows[0].Version);
                Assert.AreEqual("fake v1", rows[0].Description);
            }
        }

        [TestMethod]
        public void Run_Twice_IsIdempotent()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var v1 = new FakeV1();

                MigrationRunner.Run(ctx, new IMigration[] { v1 });
                var applied2 = MigrationRunner.Run(ctx, new IMigration[] { v1 });

                Assert.AreEqual(1, v1.UpCallCount, "Повторный запуск не должен вызывать Up снова");
                Assert.AreEqual(0, applied2.Count, "Второй прогон ничего не применяет");
                Assert.AreEqual(1, ctx.SchemaVersions.AsNoTracking().Count());
            }
        }

        [TestMethod]
        public void Run_WithNewerMigration_AppliesOnlyTheNewOne()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var v1 = new FakeV1();
                var v2 = new FakeV2();

                MigrationRunner.Run(ctx, new IMigration[] { v1 });
                Assert.AreEqual(1, v1.UpCallCount);
                Assert.AreEqual(0, v2.UpCallCount);

                var applied2 = MigrationRunner.Run(ctx, new IMigration[] { v1, v2 });

                Assert.AreEqual(1, v1.UpCallCount, "v1 не должна применяться повторно");
                Assert.AreEqual(1, v2.UpCallCount, "v2 должна примениться один раз");
                CollectionAssert.AreEqual(new[] { 2 }, applied2);

                var versions = ctx.SchemaVersions.AsNoTracking().OrderBy(v => v.Version).Select(v => v.Version).ToList();
                CollectionAssert.AreEqual(new[] { 1, 2 }, versions);
            }
        }

        [TestMethod]
        public void Run_AppliesInAscendingOrder()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var v1 = new FakeV1();
                var v2 = new FakeV2();

                // Передаём в обратном порядке — раннер должен сам отсортировать.
                var applied = MigrationRunner.Run(ctx, new IMigration[] { v2, v1 });

                CollectionAssert.AreEqual(new[] { 1, 2 }, applied,
                    "Миграции должны применяться по возрастанию Version");
            }
        }

        [TestMethod]
        public void Run_WhenMigrationThrows_RollsBackAndDoesNotRecordVersion()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                var v1 = new FakeV1();
                var bad = new FailingMigration();

                MigrationRunner.Run(ctx, new IMigration[] { v1 });

                // v2 падает — должен быть откат, SchemaVersion для v2 НЕ появляется,
                // таблица TFail тоже не должна остаться.
                Assert.ThrowsException<InvalidOperationException>(() =>
                    MigrationRunner.Run(ctx, new IMigration[] { v1, bad }));

                var versions = ctx.SchemaVersions.AsNoTracking().Select(v => v.Version).ToList();
                CollectionAssert.AreEqual(new[] { 1 }, versions,
                    "После отката должна остаться только v1");

                // Проверяем, что TFail откатилась — запрашиваем список таблиц сырым SQL.
                var names = ctx.Database
                    .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table' AND name='TFail'")
                    .ToList();
                Assert.AreEqual(0, names.Count, "Таблица TFail должна быть удалена откатом транзакции");
            }
        }

        [TestMethod]
        public void Run_RejectsNonPositiveVersion()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                Assert.ThrowsException<InvalidOperationException>(() =>
                    MigrationRunner.Run(ctx, new IMigration[] { new BadVersionMigration() }));
            }
        }

        [TestMethod]
        public void Run_OnExistingDatabaseWithoutSchemaVersion_IsSafe()
        {
            // Симулируем «старую» установку: таблицы уже существуют,
            // но SchemaVersion отсутствует. V001 использует CREATE TABLE IF NOT EXISTS,
            // поэтому должна пройти без ошибок и зарегистрироваться.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                // Прогоняем V001, чтобы получить «старую» схему с уже существующими таблицами,
                // затем удаляем запись v1 из SchemaVersion — эмулируем установку до миграций.
                MigrationRunner.Run(ctx, new IMigration[] { new V001_InitialSchema() });
                ctx.Database.ExecuteSqlRaw("DELETE FROM SchemaVersion");

                // Положим какие-то данные, чтобы убедиться, что V001 их не трогает.
                ctx.MaterialTypes.Add(new MaterialType { Name = "Сталь 3" });
                ctx.SaveChanges();
                var beforeCount = ctx.MaterialTypes.AsNoTracking().Count();

                // Важно: отвязать отслеживаемые entities, чтобы следующий SaveChanges
                // из MigrationRunner не наткнулся на stale state.
                foreach (var entry in ctx.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;

                var applied = MigrationRunner.Run(ctx, new IMigration[] { new V001_InitialSchema() });

                CollectionAssert.AreEqual(new[] { 1 }, applied,
                    "V001 должна зарегистрироваться даже на существующей схеме");
                Assert.AreEqual(beforeCount, ctx.MaterialTypes.AsNoTracking().Count(),
                    "Существующие данные должны сохраниться");
            }
        }

        [TestMethod]
        public void Run_NullConnection_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                MigrationRunner.Run(null!));
        }

        [TestMethod]
        public void GetAllMigrations_ContainsV001()
        {
            var all = MigrationRunner.GetAllMigrations();
            Assert.IsTrue(all.Any(m => m.Version == 1),
                "Канонический список должен содержать V001_InitialSchema");
        }

        [TestMethod]
        public void GetAllMigrations_HasUniqueVersions()
        {
            var all = MigrationRunner.GetAllMigrations();
            var versions = all.Select(m => m.Version).ToList();
            var distinct = versions.Distinct().ToList();
            CollectionAssert.AreEqual(versions, distinct,
                "Номера миграций должны быть уникальны");
        }
    }
}
