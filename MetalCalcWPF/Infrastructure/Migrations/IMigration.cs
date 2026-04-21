using SQLite;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// Контракт одной миграции БД.
    ///
    /// Как добавить новую миграцию:
    /// 1. Создай класс Vxxx_Description : IMigration с новым номером Version.
    /// 2. Реализуй Up(db) — SQL-команды или db.CreateTable / db.Execute("ALTER TABLE ...").
    /// 3. Добавь её в список в MigrationRunner.GetAllMigrations() (или передай явно).
    /// 4. НИКОГДА не меняй уже выпущенные миграции — только добавляй новые.
    /// </summary>
    public interface IMigration
    {
        /// <summary>Целочисленный номер. Должен расти монотонно (1, 2, 3 …).</summary>
        int Version { get; }

        /// <summary>Короткое человекочитаемое описание. Пишется в SchemaVersion.Description.</summary>
        string Description { get; }

        /// <summary>Накатить изменения. Вызывается внутри транзакции.</summary>
        void Up(SQLiteConnection db);
    }
}
