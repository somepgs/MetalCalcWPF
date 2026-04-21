using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// Идемпотентный раннер миграций БД.
    ///
    /// На каждом старте приложения:
    /// 1. Создаёт таблицу SchemaVersion (если её нет).
    /// 2. Смотрит, какие миграции уже применены.
    /// 3. Последовательно (по возрастанию Version) применяет недостающие миграции —
    ///    каждую в отдельной транзакции. Если миграция падает — откат, бросаем исключение.
    /// 4. Для совместимости со старыми БД: миграция V001 = CREATE TABLE IF NOT EXISTS …
    ///    поэтому на существующих установках она проходит без побочных эффектов и
    ///    регистрируется как применённая.
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
            // new V003_Xxx(),
        };

        /// <summary>
        /// Применяет все недостающие миграции к указанному соединению.
        /// </summary>
        /// <param name="db">Открытое соединение SQLite.</param>
        /// <param name="migrations">Полный список миграций (обычно <see cref="GetAllMigrations"/>).</param>
        /// <returns>Список номеров миграций, применённых в этом вызове.</returns>
        public static List<int> Run(SQLiteConnection db, IEnumerable<IMigration>? migrations = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            db.CreateTable<SchemaVersion>();

            var applied = new HashSet<int>(db.Table<SchemaVersion>().Select(v => v.Version));
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

                try
                {
                    db.RunInTransaction(() =>
                    {
                        migration.Up(db);
                        db.Insert(new SchemaVersion
                        {
                            Version = migration.Version,
                            AppliedAt = DateTime.UtcNow,
                            Description = migration.Description ?? string.Empty,
                        });
                    });
                    justApplied.Add(migration.Version);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Не удалось применить миграцию v{migration.Version} ({migration.Description}): {ex.Message}",
                        ex);
                }
            }

            return justApplied;
        }
    }
}
