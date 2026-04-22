using System;

namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Запись о применённой миграции. Таблица ведётся MigrationRunner-ом,
    /// руками ничего не добавляем.
    /// </summary>
    public class SchemaVersion
    {
        // PK без автоинкремента — версии задаются вручную в миграциях.
        // Конфигурация в AppDbContext: e.HasKey(x => x.Version) + ValueGeneratedNever().
        public int Version { get; set; }

        public DateTime AppliedAt { get; set; }

        public string Description { get; set; } = string.Empty;
    }
}
