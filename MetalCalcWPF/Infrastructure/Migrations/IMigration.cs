using MetalCalcWPF.Infrastructure.Persistence;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// Контракт одной миграции БД.
    ///
    /// Как добавить новую миграцию:
    /// 1. Создай класс Vxxx_Description : IMigration с новым номером Version.
    /// 2. Реализуй Up(ctx) — <c>ctx.Database.ExecuteSqlRaw(...)</c> для DDL/ALTER,
    ///    либо LINQ (<c>ctx.Set&lt;T&gt;().Add(...)</c>) для сидов.
    /// 3. Добавь её в список в MigrationRunner.GetAllMigrations() (или передай явно).
    /// 4. НИКОГДА не меняй уже выпущенные миграции — только добавляй новые.
    ///
    /// До Спринта 2.3-0 этот контракт принимал <c>SQLiteConnection</c> от sqlite-net-pcl.
    /// Переведён на <see cref="AppDbContext"/> в рамках миграции на EF Core —
    /// модели и схема данных остались те же, под капотом работает та же native SQLite.
    /// </summary>
    public interface IMigration
    {
        /// <summary>Целочисленный номер. Должен расти монотонно (1, 2, 3 …).</summary>
        int Version { get; }

        /// <summary>Короткое человекочитаемое описание. Пишется в SchemaVersion.Description.</summary>
        string Description { get; }

        /// <summary>
        /// Накатить изменения. Вызывается <see cref="MigrationRunner"/>-ом внутри
        /// транзакции — коммит/откат делает раннер, миграция только выполняет DDL/seed.
        /// </summary>
        void Up(AppDbContext ctx);
    }
}
