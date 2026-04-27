using System;
using System.Collections.Generic;

namespace MetalCalcWPF.Models.Reporting
{
    /// <summary>
    /// Агрегаты по выбранному периоду истории заказов — то, что реально
    /// интересует руководство: сколько заказов, на какую сумму, в какой
    /// пропорции по типам операций, средний чек.
    ///
    /// <para>Намеренно иммутабельный DTO без ссылок на БД — собирается один раз
    /// <see cref="Services.Interfaces.IReportingService.BuildSummary"/>
    /// и дальше используется и в UI (бейджи над таблицей), и в Excel-экспорте.</para>
    /// </summary>
    public class ReportSummary
    {
        /// <summary>Начало отчётного периода (включительно).</summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>Конец отчётного периода (исключительно).</summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>Всего заказов в периоде.</summary>
        public int TotalOrders { get; set; }

        /// <summary>Суммарная выручка за период (тг).</summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>Средний чек за период (тг). 0, если <see cref="TotalOrders"/> = 0.</summary>
        public decimal AverageOrderValue { get; set; }

        /// <summary>Количество выполненных заказов в периоде (CompletedDate != null).</summary>
        public int CompletedCount { get; set; }

        /// <summary>Количество заказов в очереди (CompletedDate == null).</summary>
        public int PendingCount { get; set; }

        /// <summary>Разбивка по типам операций (Laser / Bending / Welding / …).</summary>
        public List<OperationBreakdown> ByOperation { get; set; } = new List<OperationBreakdown>();
    }

    /// <summary>
    /// Строка в разбивке по типу операции. Сортируется по убыванию выручки,
    /// чтобы самое «денежное» было сверху — так удобнее читать отчёт.
    /// </summary>
    public class OperationBreakdown
    {
        public string OperationType { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Revenue { get; set; }

        /// <summary>Доля в общей выручке (0..1). В пустом периоде = 0.</summary>
        public double ShareOfRevenue { get; set; }
    }
}
