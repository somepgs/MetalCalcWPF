using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SQLite;
using MetalCalcWPF.Infrastructure.Migrations;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты раннера миграций. Все тесты работают с in-memory SQLite (:memory:),
    /// поэтому не оставляют файлов на диске и не зависят от состояния реальной БД.
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
            public void Up(SQLiteConnection db)
            {
                UpCallCount++;
                db.Execute("CREATE TABLE IF NOT EXISTS T1 (Id INTEGER PRIMARY KEY)");
            }
        }

        private class FakeV2 : IMigration
        {
            public int Version => 2;
            public string Description => "fake v2";
            public int UpCallCount;
            public void Up(SQLiteConnection db)
            {
                UpCallCount++;
                db.Execute("CREATE TABLE IF NOT EXISTS T2 (Id INTEGER PRIMARY KEY)");
            }
        }

        private class FailingMigration : IMigration
        {
            public int Version => 2;
            public string Description => "will throw";
            public void Up(SQLiteConnection db)
            {
                // Создадим таблицу, потом сорвёмся — транзакция должна откатить.
                db.Execute("CREATE TABLE IF NOT EXISTS TFail (Id INTEGER PRIMARY KEY)");
                throw new InvalidOperationException("boom");
            }
        }

        private class BadVersionMigration : IMigration
        {
            public int Version => 0;
            public string Description => "bad";
            public void Up(SQLiteConnection db) { }
        }

        // --- Тесты ---

        [TestMethod]
        public void Run_FreshDatabase_AppliesV1AndRecordsIt()
        {
            using var db = new SQLiteConnection(":memory:");
            var v1 = new FakeV1();

            var applied = MigrationRunner.Run(db, new IMigration[] { v1 });

            Assert.AreEqual(1, v1.UpCallCount, "Up должен быть вызван ровно один раз");
            CollectionAssert.AreEqual(new[] { 1 }, applied);

            var rows = db.Table<SchemaVersion>().ToList();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(1, rows[0].Version);
            Assert.AreEqual("fake v1", rows[0].Description);
        }

        [TestMethod]
        public void Run_Twice_IsIdempotent()
        {
            using var db = new SQLiteConnection(":memory:");
            var v1 = new FakeV1();

            MigrationRunner.Run(db, new IMigration[] { v1 });
            var applied2 = MigrationRunner.Run(db, new IMigration[] { v1 });

            Assert.AreEqual(1, v1.UpCallCount, "Повторный запуск не должен вызывать Up снова");
            Assert.AreEqual(0, applied2.Count, "Второй прогон ничего не применяет");
            Assert.AreEqual(1, db.Table<SchemaVersion>().Count());
        }

        [TestMethod]
        public void Run_WithNewerMigration_AppliesOnlyTheNewOne()
        {
            using var db = new SQLiteConnection(":memory:");
            var v1 = new FakeV1();
            var v2 = new FakeV2();

            // Первый прогон — только v1
            MigrationRunner.Run(db, new IMigration[] { v1 });
            Assert.AreEqual(1, v1.UpCallCount);
            Assert.AreEqual(0, v2.UpCallCount);

            // Второй прогон — добавляем v2, v1 уже применена
            var applied2 = MigrationRunner.Run(db, new IMigration[] { v1, v2 });

            Assert.AreEqual(1, v1.UpCallCount, "v1 не должна применяться повторно");
            Assert.AreEqual(1, v2.UpCallCount, "v2 должна примениться один раз");
            CollectionAssert.AreEqual(new[] { 2 }, applied2);

            var versions = db.Table<SchemaVersion>().OrderBy(v => v.Version).Select(v => v.Version).ToList();
            CollectionAssert.AreEqual(new[] { 1, 2 }, versions);
        }

        [TestMethod]
        public void Run_AppliesInAscendingOrder()
        {
            using var db = new SQLiteConnection(":memory:");
            var v1 = new FakeV1();
            var v2 = new FakeV2();

            // Передаём в обратном порядке — раннер должен сам отсортировать
            var applied = MigrationRunner.Run(db, new IMigration[] { v2, v1 });

            CollectionAssert.AreEqual(new[] { 1, 2 }, applied,
                "Миграции должны применяться по возрастанию Version");
        }

        [TestMethod]
        public void Run_WhenMigrationThrows_RollsBackAndDoesNotRecordVersion()
        {
            using var db = new SQLiteConnection(":memory:");
            var v1 = new FakeV1();
            var bad = new FailingMigration();

            MigrationRunner.Run(db, new IMigration[] { v1 });

            // v2 падает — должен быть откат, SchemaVersion для v2 НЕ появляется,
            // таблица TFail тоже не должна остаться.
            Assert.ThrowsException<InvalidOperationException>(() =>
                MigrationRunner.Run(db, new IMigration[] { v1, bad }));

            var versions = db.Table<SchemaVersion>().Select(v => v.Version).ToList();
            CollectionAssert.AreEqual(new[] { 1 }, versions,
                "После отката должна остаться только v1");

            // Проверяем, что TFail откатилась.
            var exists = db.ExecuteScalar<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='TFail'");
            Assert.IsNull(exists, "Таблица TFail должна быть удалена откатом транзакции");
        }

        [TestMethod]
        public void Run_RejectsNonPositiveVersion()
        {
            using var db = new SQLiteConnection(":memory:");

            Assert.ThrowsException<InvalidOperationException>(() =>
                MigrationRunner.Run(db, new IMigration[] { new BadVersionMigration() }));
        }

        [TestMethod]
        public void Run_OnExistingDatabaseWithoutSchemaVersion_IsSafe()
        {
            // Симулируем «старую» установку: таблицы уже существуют,
            // но SchemaVersion отсутствует. V001 использует CreateTable (IF NOT EXISTS),
            // поэтому должна пройти без ошибок и зарегистрироваться.
            using var db = new SQLiteConnection(":memory:");

            db.CreateTable<WorkshopSettings>();
            db.CreateTable<MaterialType>();
            db.CreateTable<MaterialProfile>();
            db.CreateTable<BendingProfile>();
            db.CreateTable<WeldingProfile>();
            db.CreateTable<RolledProfile>();
            db.CreateTable<OrderHistory>();

            // Положим какие-то данные, чтобы убедиться, что V001 их не трогает.
            db.Insert(new MaterialType { Name = "Сталь 3" });
            var beforeCount = db.Table<MaterialType>().Count();

            var applied = MigrationRunner.Run(db, new IMigration[] { new V001_InitialSchema() });

            CollectionAssert.AreEqual(new[] { 1 }, applied,
                "V001 должна зарегистрироваться даже на существующей схеме");
            Assert.AreEqual(beforeCount, db.Table<MaterialType>().Count(),
                "Существующие данные должны сохраниться");
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
