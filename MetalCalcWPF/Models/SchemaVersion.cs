using System;
using SQLite;

namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Запись о применённой миграции. Таблица ведётся MigrationRunner-ом,
    /// руками ничего не добавляем.
    /// </summary>
    public class SchemaVersion
    {
        [PrimaryKey]
        public int Version { get; set; }

        public DateTime AppliedAt { get; set; }

        public string Description { get; set; } = string.Empty;
    }
}
