using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// Идемпотентный раннер миграций БД на EF Core.
    ///
    /// На каждом старте приложения:
    /// 1. Создаёт таблицу SchemaVersion (CREATE TABLE IF NOT EXISTS).
    /// 2. Смотрит, какие миграции уже применены.
    /// 3. Последовательно (по возрастанию Version) применяет недостающие миграции —
    ///    каждую в отдельной транзакции через <c>ctx.Database.BeginTransaction()</c>.
    ///    Если миграция падает — откат, бросаем исключение.
    /// 4. Для совместимости со старыми БД: миграция V001 = CREATE TABLE IF NOT EXISTS …
    ///    поэтому на существующих установках она проходит без побочных эффектов и
    ///    регистрируется как применённая.
    ///
    /// <para>До Спринта 2.3-0 раннер работал на <c>SQLiteConnection</c> от sqlite-net-pcl.
    /// После перехода на EF Core под капотом используется та же native библиотека
    /// SQLitePCLRaw, поэтому бинарный формат файла БД не меняется и существующие
    /// <c>workshop.db</c> продолжают открываться без конвертации.</para>
    /// </summary>
    public static class MigrationRunner
    {
        /// <summary>
        /// Канонический список всех миграций приложения.
        /// Новые миграции добавляй ТОЛЬКО в конец, ЕСЛИ Version увеличивается.
        /// </summary>
        public static IReadOnlyList<IMigration> GetAllMigrations() => new IMigration[]
        {
            new V001_InitialSchema(),
            new V002_AddCuttingMachines(),
            new V003_AddLaserMinutePrices(),
            // new V004_Xxx(),
        };

        /// <summary>
        /// Применяет все недостающие миграции к указанному контексту.
        /// </summary>
        /// <param name="ctx">Открытый <see cref="AppDbContext"/>. Соединение не закрывается.</param>
        /// <param name="migrations">Полный список миграций (обычно <see cref="GetAllMigrations"/>).</param>
        /// <returns>Список номеров миграций, применённых в этом вызове.</returns>
        public static List<int> Run(AppDbContext ctx, IEnumerable<IMigration>? migrations = null)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            // 1) Гарантируем наличие SchemaVersion. Колонки заданы вручную
            //    в стиле старых sqlite-net CreateTable-ов (integer / bigint / varchar),
            //    чтобы существующие БД с точно такой же таблицей проходили без изменений.
            ctx.Database.ExecuteSqlRaw(
                "CREATE TABLE IF NOT EXISTS \"SchemaVersion\" (" +
                "\"Version\" integer primary key not null," +
                "\"AppliedAt\" bigint not null," +
                "\"Description\" varchar)");

            // 2) Считываем уже применённые версии. AsNoTracking — чтобы change tracker
            //    не держал старые записи, мешающие последующим SaveChanges внутри миграций.
            var applied = new HashSet<int>(
                ctx.SchemaVersions.AsNoTracking().Select(v => v.Version).ToList());

            var toApply = (migrations ?? GetAllMigrations())
                .OrderBy(m => m.Version)
                .Where(m => !applied.Contains(m.Version))
                .ToList();

            var justApplied = new List<int>();

            foreach (var migration in toApply)
            {
                if (migration.Version <= 0)
                    throw new InvalidOperationException(
                        $"Номер миграции должен быть > 0, а получил {migration.Version} в {migration.GetType().Name}.");

                using var tx = ctx.Database.BeginTransaction();
                try
                {
                    migration.Up(ctx);

                    ctx.SchemaVersions.Add(new SchemaVersion
                    {
                        Version = migration.Version,
                        AppliedAt = DateTime.UtcNow,
                        Description = migration.Description ?? string.Empty,
                    });
                    ctx.SaveChanges();

                    tx.Commit();
                    justApplied.Add(migration.Version);
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { /* rollback best-effort */ }
                    // EF Core оставит незакоммиченные entities в tracker —
                    // отвязываем всё, чтобы следующие миграции не наткнулись
                    // на «фантом» SchemaVersion.
                    foreach (var entry in ctx.ChangeTracker.Entries().ToList())
                        entry.State = EntityState.Detached;

                    throw new InvalidOperationException(
                        $"Не удалось применить миграцию v{migration.Version} ({migration.Description}): {ex.Message}",
                        ex);
                }
            }

            return justApplied;
        }
    }
}
