using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v6 — добавляет в OrderHistory 7 полей заявки для workflow заказов.
    ///
    /// Новые колонки (Этап 3 плана):
    /// - Priority             (int)     — срочность; default = 1 (Normal)
    /// - Quantity             (int)     — кол-во деталей; default = 0 (для legacy)
    /// - MassKg               (float)   — масса партии в кг
    /// - ApplicantName        (varchar) — ФИО заявителя (снапшот)
    /// - ApplicantWorkshopName(varchar) — цех заявителя (снапшот)
    /// - AcceptorName         (varchar) — ФИО принявшего заказ
    /// - MaterialName         (varchar) — марка материала (снапшот)
    ///
    /// Хранение снапшотов (а не FK) гарантирует, что отчёты прошлых периодов
    /// не «портятся» при правках в справочниках Workshop / Person / MaterialType.
    ///
    /// Стратегия — как в V003/V004: HasColumn + ALTER TABLE ADD COLUMN с DEFAULT.
    /// Идемпотентно, безопасно для legacy БД, ничего не пересоздаёт.
    /// </summary>
    public class V006_AddOrderApplicationFields : IMigration
    {
        public int Version => 6;

        public string Description =>
            "OrderHistory: добавлены поля заявки (Priority, Quantity, MassKg, ФИО, цех, материал).";

        public void Up(AppDbContext ctx)
        {
            const string table = "OrderHistory";

            AddColumnIfMissing(ctx, table, "Priority",              "integer NOT NULL DEFAULT 1");
            AddColumnIfMissing(ctx, table, "Quantity",              "integer NOT NULL DEFAULT 0");
            AddColumnIfMissing(ctx, table, "MassKg",                "float NOT NULL DEFAULT 0");
            AddColumnIfMissing(ctx, table, "ApplicantName",         "varchar");
            AddColumnIfMissing(ctx, table, "ApplicantWorkshopName", "varchar");
            AddColumnIfMissing(ctx, table, "AcceptorName",          "varchar");
            AddColumnIfMissing(ctx, table, "MaterialName",          "varchar");
        }

        private static void AddColumnIfMissing(AppDbContext ctx, string table, string column, string sqlType)
        {
            if (HasColumn(ctx, table, column)) return;

            ctx.Database.ExecuteSqlRaw(
                "ALTER TABLE \"" + table + "\" ADD COLUMN \"" + column + "\" " + sqlType);
        }

        private static bool HasColumn(AppDbContext ctx, string table, string column)
        {
            var names = ctx.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"" + table + "\")")
                .ToList();

            return names.Any(n => string.Equals(n, column, StringComparison.OrdinalIgnoreCase));
        }
    }
}
