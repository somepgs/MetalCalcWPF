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
    /// Тесты миграции v7 — чистка технических подписей в Workshop.Notes,
    /// оставшихся от первой редакции V005.
    /// </summary>
    [TestClass]
    public class CleanupSeedNotesMigrationTests
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
        public void V007_ClearsTechnicalNotesFromV5Seed()
        {
            // Эмулируем БД, где V005 уже посеяла цеха со старым техническим текстом.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[] { new V001_InitialSchema() });

                ctx.Workshops.AddRange(
                    new Workshop { Name = "Цех СВ", Kind = WorkshopKind.Internal, IsActive = true,
                                   Notes = "Создан миграцией v5." },
                    new Workshop { Name = "Цех металлообработки", Kind = WorkshopKind.Internal, IsActive = true,
                                   Notes = "Наш цех. Создан миграцией v5." },
                    new Workshop { Name = "Внешний клиент", Kind = WorkshopKind.ExternalClient, IsActive = true,
                                   Notes = "Договор №123, контакт: Иванов" }
                );
                ctx.SaveChanges();

                // Прогоняем V007 — она должна очистить только технические подписи.
                MigrationRunner.Run(ctx, new IMigration[] { new V007_CleanupSeedNotes() });

                var workshops = ctx.Workshops.AsNoTracking().ToList();
                Assert.AreEqual("",          workshops.Single(w => w.Name == "Цех СВ").Notes,
                    "Чистый сид → пустой Notes");
                Assert.AreEqual("Наш цех",   workshops.Single(w => w.Name == "Цех металлообработки").Notes,
                    "С 'Наш цех' оставляем только осмысленную часть");
                Assert.AreEqual("Договор №123, контакт: Иванов",
                    workshops.Single(w => w.Name == "Внешний клиент").Notes,
                    "Пользовательский Notes (без маркера 'миграцией v5') не трогаем");
            }
        }

        [TestMethod]
        public void V007_OnFreshDatabase_NoOp()
        {
            // На свежей БД V005 уже сидит с пустыми Notes — V007 ничего не находит.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V005_AddWorkshopAndPerson(),
                    new V007_CleanupSeedNotes(),
                });

                var notes = ctx.Workshops.AsNoTracking().Select(w => w.Notes).ToList();
                Assert.IsTrue(notes.All(n => !n.Contains("миграцией")),
                    "На свежем сиде ни в одной заметке не должно быть слова 'миграцией'");
            }
        }

        [TestMethod]
        public void V007_IsIdempotent_SecondRunDoesNotCorrupt()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[] { new V001_InitialSchema() });
                ctx.Workshops.Add(new Workshop
                {
                    Name = "Цех X",
                    Kind = WorkshopKind.Internal,
                    IsActive = true,
                    Notes = "Создан миграцией v5.",
                });
                ctx.SaveChanges();

                MigrationRunner.Run(ctx, new IMigration[] { new V007_CleanupSeedNotes() });
                var afterFirst = ctx.Workshops.AsNoTracking().Single().Notes;

                MigrationRunner.Run(ctx, new IMigration[] { new V007_CleanupSeedNotes() });
                var afterSecond = ctx.Workshops.AsNoTracking().Single().Notes;

                Assert.AreEqual(afterFirst, afterSecond);
                Assert.AreEqual("", afterSecond);
            }
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV007InOrder()
        {
            var versions = MigrationRunner.GetAllMigrations().Select(m => m.Version).ToList();
            CollectionAssert.Contains(versions, 7);
            Assert.IsTrue(versions.IndexOf(6) < versions.IndexOf(7),
                "V006 должна идти до V007");
        }
    }
}
