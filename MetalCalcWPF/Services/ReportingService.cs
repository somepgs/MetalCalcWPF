using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using MetalCalcWPF.Models;
using MetalCalcWPF.Models.Reporting;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services
{
    /// <summary>
    /// Реализация отчётности. Хранит всю знания о структуре отчёта в одном месте,
    /// чтобы MainViewModel не обрастал Excel-кодом.
    ///
    /// ClosedXML уже есть в проекте — не добавляем новых зависимостей.
    /// </summary>
    public class ReportingService : IReportingService
    {
        public ReportSummary BuildSummary(
            IReadOnlyList<OrderHistory> orders,
            DateTime periodStart,
            DateTime periodEnd)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));

            var summary = new ReportSummary
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalPrice),
            };

            summary.AverageOrderValue = orders.Count > 0
                ? summary.TotalRevenue / orders.Count
                : 0m;

            // Разбивка по 4 cost-колонкам (миграция v4) — теперь источник правды для
            // «на чём цех заработал» это поля MaterialCost/LaserCost/BendingCost/WeldingCost
            // в OrderHistory, а не строковый OperationType. Доля считается от TotalRevenue,
            // поэтому исторические заказы до v4 (где cost-поля = 0) приведут к тому, что
            // сумма долей будет < 100% — это сигнал руководству, что часть выручки
            // не размечена.
            var breakdown = new[]
            {
                BuildOpRow(orders, "Металл", o => o.MaterialCost, summary.TotalRevenue),
                BuildOpRow(orders, "Лазер",  o => o.LaserCost,    summary.TotalRevenue),
                BuildOpRow(orders, "Гибка",  o => o.BendingCost,  summary.TotalRevenue),
                BuildOpRow(orders, "Сварка", o => o.WeldingCost,  summary.TotalRevenue),
            };

            summary.ByOperation = breakdown
                .Where(b => b.Revenue > 0)
                .OrderByDescending(b => b.Revenue)
                .ToList();

            return summary;
        }

        private static OperationBreakdown BuildOpRow(
            IReadOnlyList<OrderHistory> orders,
            string label,
            Func<OrderHistory, decimal> field,
            decimal totalRevenue)
        {
            decimal sum = orders.Sum(field);
            return new OperationBreakdown
            {
                OperationType = label,
                Count = orders.Count(o => field(o) > 0),
                Revenue = sum,
                ShareOfRevenue = totalRevenue > 0 ? (double)(sum / totalRevenue) : 0d,
            };
        }

        public void ExportToExcel(
            IReadOnlyList<OrderHistory> orders,
            ReportSummary summary,
            string filePath)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath пуст", nameof(filePath));

            using var workbook = new XLWorkbook();

            WriteSummarySheet(workbook, summary);
            WriteOrdersSheet(workbook, orders, summary);

            workbook.SaveAs(filePath);
        }

        // ======================================================================
        // Листы Excel
        // ======================================================================

        /// <summary>
        /// Лист «Итоги» идёт первым — руководству в первую очередь нужна сводка,
        /// а не построчные данные.
        /// </summary>
        private static void WriteSummarySheet(XLWorkbook workbook, ReportSummary summary)
        {
            var ws = workbook.Worksheets.Add("Итоги");

            // Шапка отчёта.
            ws.Cell(1, 1).Value = "Отчёт по заказам";
            ws.Range(1, 1, 1, 4).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = "Период:";
            ws.Cell(2, 2).Value = FormatPeriod(summary.PeriodStart, summary.PeriodEnd);
            ws.Cell(2, 1).Style.Font.Bold = true;

            ws.Cell(3, 1).Value = "Сформировано:";
            ws.Cell(3, 2).Value = DateTime.Now;
            ws.Cell(3, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            ws.Cell(3, 1).Style.Font.Bold = true;

            // Блок ключевых цифр.
            const int kpiRow = 5;
            ws.Cell(kpiRow, 1).Value = "Ключевые показатели";
            ws.Cell(kpiRow, 1).Style.Font.Bold = true;
            ws.Cell(kpiRow, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Range(kpiRow, 1, kpiRow, 2).Merge();

            ws.Cell(kpiRow + 1, 1).Value = "Всего заказов";
            ws.Cell(kpiRow + 1, 2).Value = summary.TotalOrders;

            ws.Cell(kpiRow + 2, 1).Value = "Суммарная выручка (₸)";
            ws.Cell(kpiRow + 2, 2).Value = summary.TotalRevenue;
            ws.Cell(kpiRow + 2, 2).Style.NumberFormat.Format = "#,##0";

            ws.Cell(kpiRow + 3, 1).Value = "Средний чек (₸)";
            ws.Cell(kpiRow + 3, 2).Value = summary.AverageOrderValue;
            ws.Cell(kpiRow + 3, 2).Style.NumberFormat.Format = "#,##0";

            // Разбивка по операциям.
            var opStartRow = kpiRow + 5;
            ws.Cell(opStartRow, 1).Value = "Разбивка по типам операций";
            ws.Cell(opStartRow, 1).Style.Font.Bold = true;
            ws.Cell(opStartRow, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Range(opStartRow, 1, opStartRow, 4).Merge();

            var headerRow = opStartRow + 1;
            ws.Cell(headerRow, 1).Value = "Тип операции";
            ws.Cell(headerRow, 2).Value = "Заказов";
            ws.Cell(headerRow, 3).Value = "Выручка (₸)";
            ws.Cell(headerRow, 4).Value = "Доля";
            ws.Range(headerRow, 1, headerRow, 4).Style.Font.Bold = true;
            ws.Range(headerRow, 1, headerRow, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

            var dataRow = headerRow + 1;
            foreach (var op in summary.ByOperation)
            {
                ws.Cell(dataRow, 1).Value = op.OperationType;
                ws.Cell(dataRow, 2).Value = op.Count;
                ws.Cell(dataRow, 3).Value = op.Revenue;
                ws.Cell(dataRow, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(dataRow, 4).Value = op.ShareOfRevenue;
                ws.Cell(dataRow, 4).Style.NumberFormat.Format = "0.0%";
                dataRow++;
            }

            // Строка «Итого» замыкает разбивку. Формулы — чтобы Excel сам пересчитал,
            // если пользователь поправит цифры в ячейках вручную (редкий, но нужный кейс).
            if (summary.ByOperation.Count > 0)
            {
                var firstDataRow = headerRow + 1;
                var lastDataRow = dataRow - 1;
                ws.Cell(dataRow, 1).Value = "Итого";
                ws.Cell(dataRow, 2).FormulaA1 = $"=SUM(B{firstDataRow}:B{lastDataRow})";
                ws.Cell(dataRow, 3).FormulaA1 = $"=SUM(C{firstDataRow}:C{lastDataRow})";
                ws.Cell(dataRow, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(dataRow, 4).FormulaA1 = $"=SUM(D{firstDataRow}:D{lastDataRow})";
                ws.Cell(dataRow, 4).Style.NumberFormat.Format = "0.0%";
                ws.Range(dataRow, 1, dataRow, 4).Style.Font.Bold = true;
                ws.Range(dataRow, 1, dataRow, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns().AdjustToContents();
        }

        /// <summary>
        /// Лист «Заказы» — построчная детализация под отчёт руководству (Этап 3).
        ///
        /// Колонки 1..12 — управленческие (как просило руководство):
        /// №, Дата поступления, Заявитель, Цех заявителя, Принявший, Срочность,
        /// Кол-во, Материал, Операции, Масса (кг), Дата выполнения, Сумма.
        ///
        /// Колонки 13..16 — детализация cost-разбивки (для бухгалтерии и
        /// планово-экономического отдела): Металл, Лазер, Гибка, Сварка.
        /// </summary>
        private static void WriteOrdersSheet(
            XLWorkbook workbook,
            IReadOnlyList<OrderHistory> orders,
            ReportSummary summary)
        {
            var ws = workbook.Worksheets.Add("Заказы");

            // Колонки 1..13 — управленческие (полный список руководства);
            // 14..17 — детализация cost-разбивки для бухгалтерии.
            const int colCount = 17;

            ws.Cell(1, 1).Value = $"Заказы за период: {FormatPeriod(summary.PeriodStart, summary.PeriodEnd)}";
            ws.Range(1, 1, 1, colCount).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 12;

            // Заголовки таблицы — порядок колонок согласован с руководством.
            const int headerRow = 3;
            ws.Cell(headerRow, 1).Value  = "№";
            ws.Cell(headerRow, 2).Value  = "Дата поступления";
            ws.Cell(headerRow, 3).Value  = "Заявитель";
            ws.Cell(headerRow, 4).Value  = "Цех заявителя";
            ws.Cell(headerRow, 5).Value  = "Принявший";
            ws.Cell(headerRow, 6).Value  = "Срочность";
            ws.Cell(headerRow, 7).Value  = "Кол-во";
            ws.Cell(headerRow, 8).Value  = "Материал";
            ws.Cell(headerRow, 9).Value  = "Изделие";
            ws.Cell(headerRow, 10).Value = "Операции";
            ws.Cell(headerRow, 11).Value = "Масса (кг)";
            ws.Cell(headerRow, 12).Value = "Дата выполнения";   // Этап 4 заполнит
            ws.Cell(headerRow, 13).Value = "Сумма (₸)";
            ws.Cell(headerRow, 14).Value = "Металл (₸)";
            ws.Cell(headerRow, 15).Value = "Лазер (₸)";
            ws.Cell(headerRow, 16).Value = "Гибка (₸)";
            ws.Cell(headerRow, 17).Value = "Сварка (₸)";

            var headerRange = ws.Range(headerRow, 1, headerRow, colCount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Alignment.WrapText = true;
            ws.Row(headerRow).Height = 30;

            int row = headerRow + 1;
            foreach (var order in orders)
            {
                ws.Cell(row, 1).Value  = order.Id;
                ws.Cell(row, 2).Value  = order.CreatedDate;
                ws.Cell(row, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                ws.Cell(row, 3).Value  = order.ApplicantName         ?? string.Empty;
                ws.Cell(row, 4).Value  = order.ApplicantWorkshopName ?? string.Empty;
                ws.Cell(row, 5).Value  = order.AcceptorName          ?? string.Empty;
                ws.Cell(row, 6).Value  = FormatPriority(order.Priority);
                ws.Cell(row, 7).Value  = order.Quantity;
                ws.Cell(row, 8).Value  = order.MaterialName ?? string.Empty;
                ws.Cell(row, 9).Value  = order.ClientName ?? string.Empty;   // Изделие
                ws.Cell(row, 10).Value = FormatOperations(order);
                ws.Cell(row, 11).Value = order.MassKg;
                ws.Cell(row, 11).Style.NumberFormat.Format = "0.##;-0.##;-";
                // Дата выполнения (col 12) — пусто, наполняется в Этапе 4.
                ws.Cell(row, 13).Value = order.TotalPrice;
                ws.Cell(row, 14).Value = order.MaterialCost;
                ws.Cell(row, 15).Value = order.LaserCost;
                ws.Cell(row, 16).Value = order.BendingCost;
                ws.Cell(row, 17).Value = order.WeldingCost;

                // Числовой формат для денег: тире вместо нулей не загромождает глаз.
                ws.Range(row, 13, row, 17).Style.NumberFormat.Format = "#,##0;-#,##0;-";
                row++;
            }

            // Строка «Итого» — формулы SUM по числовым колонкам.
            // Кол-во (7) и Масса (11) тоже суммируются — полезно для отчёта.
            if (orders.Count > 0)
            {
                int firstData = headerRow + 1;
                int lastData = row - 1;
                ws.Cell(row, 1).Value = "Итого";

                int[] sumCols = { 7, 11, 13, 14, 15, 16, 17 };
                foreach (var col in sumCols)
                {
                    var letter = ColumnLetter(col);
                    ws.Cell(row, col).FormulaA1 = $"=SUM({letter}{firstData}:{letter}{lastData})";
                }
                ws.Cell(row, 11).Style.NumberFormat.Format = "0.##;-0.##;-";
                ws.Range(row, 13, row, 17).Style.NumberFormat.Format = "#,##0;-#,##0;-";

                ws.Range(row, 1, row, colCount).Style.Font.Bold = true;
                ws.Range(row, 1, row, colCount).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns().AdjustToContents();
            // Заморозка шапки + первой колонки — длинная таблица должна листаться удобно.
            ws.SheetView.Freeze(headerRow, 1);
        }

        /// <summary>
        /// Список выполненных операций, склеенный через «+»: «Металл+Лазер»,
        /// «Лазер+Гибка+Сварка» и т.п. Берётся из cost-полей — что не равно нулю,
        /// то и было реально сделано.
        /// <para>Fallback на лог калькулятора нужен для исторических заказов до v4,
        /// где cost-поля все нулевые: тогда хотя бы видно «Металл(...) + Лазер(...)»
        /// в исходном виде.</para>
        /// </summary>
        private static string FormatOperations(OrderHistory order)
        {
            var ops = new List<string>(4);
            if (order.MaterialCost > 0) ops.Add("Металл");
            if (order.LaserCost > 0)    ops.Add("Лазер");
            if (order.BendingCost > 0)  ops.Add("Гибка");
            if (order.WeldingCost > 0)  ops.Add("Сварка");

            return ops.Count > 0
                ? string.Join("+", ops)
                : NormalizeOperationType(order.OperationType);
        }

        /// <summary>
        /// Текстовое представление срочности для Excel — руководство не любит читать
        /// числа enum'а. На неизвестное значение возвращаем «—», чтобы файл не падал.
        /// </summary>
        private static string FormatPriority(OrderPriority priority) => priority switch
        {
            OrderPriority.Low     => "Низкая",
            OrderPriority.Normal  => "Обычная",
            OrderPriority.High    => "Высокая",
            OrderPriority.Urgent  => "Срочно",
            _ => "—"
        };

        /// <summary>Конвертация номера колонки 1..26 в букву A..Z. Достаточно для 16 колонок.</summary>
        private static string ColumnLetter(int col) => ((char)('A' + col - 1)).ToString();

        // ======================================================================
        // Хелперы
        // ======================================================================

        /// <summary>
        /// Красиво форматирует период «с … по …» для шапки отчёта.
        /// <c>endExclusive</c> отображаем как «включительно предыдущий день»,
        /// потому что руководство не ждёт программистских полуоткрытых интервалов.
        /// </summary>
        private static string FormatPeriod(DateTime start, DateTime endExclusive)
        {
            var endInclusive = endExclusive.AddDays(-1);
            if (endInclusive < start) endInclusive = start;
            return $"{start:dd.MM.yyyy} – {endInclusive:dd.MM.yyyy}";
        }

        /// <summary>
        /// <see cref="OrderHistory.OperationType"/> сейчас содержит сырой лог расчёта
        /// (многострочный), потому что MainViewModel пишет туда <c>result.Log</c>.
        /// До рефакторинга в enum — берём первую непустую строку как короткое имя.
        /// </summary>
        private static string NormalizeOperationType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            var firstLine = raw.Split('\n').FirstOrDefault()?.Trim();
            return string.IsNullOrEmpty(firstLine) ? "—" : firstLine;
        }
    }
}
