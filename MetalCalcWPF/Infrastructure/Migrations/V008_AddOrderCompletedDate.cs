using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v8 — добавляет в OrderHistory колонку CompletedDate (DateTime?, ticks)
    /// для workflow «выполнен / в очереди».
    ///
    /// <para>NULL означает «заказ ещё не выполнен» — он попадает в лист «Очередь»
    /// Excel-отчёта. Конкретное значение — момент, когда мастер нажал
    /// «📌 Отметить как выполнен» в истории.</para>
    ///
    /// Стратегия — стандартная для добавления колонок: HasColumn + ALTER TABLE.
    /// Колонка nullable, default → NULL: историческим заказам не присваиваем
    /// дату выполнения автоматически (мы не знаем, выполнены они или нет).
    /// </summary>
    public class V008_AddOrderCompletedDate : IMigration
    {
        public int Version => 8;

        public string Description =>
            "OrderHistory: добавлена дата выполнения заказа (для статуса и очереди).";

        public void Up(AppDbContext ctx)
        {
            const string table = "OrderHistory";
            if (HasColumn(ctx, table, "CompletedDate")) return;

            // bigint, как и CreatedDate — храним ticks. NULL разрешён, без DEFAULT.
            ctx.Database.ExecuteSqlRaw(
                "ALTER TABLE \"" + table + "\" ADD COLUMN \"CompletedDate\" bigint");
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
