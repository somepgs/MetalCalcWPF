using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v4 — добавляет в <see cref="OrderHistory"/> четыре cost-колонки для отчётности
    /// руководству: <see cref="OrderHistory.MaterialCost"/>, <see cref="OrderHistory.LaserCost"/>,
    /// <see cref="OrderHistory.BendingCost"/>, <see cref="OrderHistory.WeldingCost"/>.
    ///
    /// Зачем: Excel-отчёт раньше показывал только итоговую сумму заказа. Руководство
    /// хочет видеть, на чём именно цех зарабатывает — поэтому в БД нужно хранить
    /// разбивку, которая всегда есть в <c>CalculationResult</c>, но не сохранялась.
    ///
    /// Стратегия — точно как в <see cref="V003_AddLaserMinutePrices"/>:
    /// 1. Свежая БД → V001 уже создаёт OrderHistory сразу с 4 cost-колонками,
    ///    HasColumn вернёт true и ALTER TABLE не выполнится.
    /// 2. Существующая БД с 6-колонной OrderHistory → ALTER TABLE добавит
    ///    каждую колонку с DEFAULT 0. Существующие заказы получат нули
    ///    (cost-разбивка для них неизвестна — это ожидаемо).
    ///
    /// ALTER TABLE ADD COLUMN в SQLite не блокирует, не пересоздаёт таблицу
    /// и не теряет данные.
    /// </summary>
    public class V004_AddOrderCostBreakdown : IMigration
    {
        public int Version => 4;

        public string Description =>
            "Cost-разбивка в OrderHistory: Material/Laser/Bending/WeldingCost для отчётности.";

        public void Up(AppDbContext ctx)
        {
            const string table = "OrderHistory";

            AddColumnIfMissing(ctx, table, "MaterialCost");
            AddColumnIfMissing(ctx, table, "LaserCost");
            AddColumnIfMissing(ctx, table, "BendingCost");
            AddColumnIfMissing(ctx, table, "WeldingCost");
        }

        private static void AddColumnIfMissing(AppDbContext ctx, string table, string column)
        {
            if (HasColumn(ctx, table, column)) return;

            ctx.Database.ExecuteSqlRaw(
                "ALTER TABLE \"" + table + "\" ADD COLUMN \"" + column + "\" decimal NOT NULL DEFAULT 0");
        }

        /// <summary>
        /// PRAGMA table_info — единственный надёжный способ узнать набор колонок
        /// существующей таблицы в SQLite. Имена в кавычках безопасны: они зашиты
        /// в коде миграций, никаких пользовательских данных в DDL не уходит.
        /// </summary>
        private static bool HasColumn(AppDbContext ctx, string table, string column)
        {
            var names = ctx.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"" + table + "\")")
                .ToList();

            return names.Any(n => string.Equals(n, column, StringComparison.OrdinalIgnoreCase));
        }
    }
}
