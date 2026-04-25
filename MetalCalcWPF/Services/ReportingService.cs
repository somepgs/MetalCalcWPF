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

            // Разбивка по OperationType. NB: поле может содержать многострочный
            // лог расчёта (см. MainViewModel.Calculate → result.Log) — для отчёта
            // выделяем первую строку как «короткое имя операции». Когда почистим
            // OperationType до enum, это место упростится до obj.OperationType.
            summary.ByOperation = orders
                .GroupBy(o => NormalizeOperationType(o.OperationType))
                .Select(g => new OperationBreakdown
                {
                    OperationType = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(o => o.TotalPrice),
                    ShareOfRevenue = summary.TotalRevenue > 0
                        ? (double)(g.Sum(o => o.TotalPrice) / summary.TotalRevenue)
                        : 0d,
                })
                .OrderByDescending(b => b.Revenue)
                .ToList();

            return summary;
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
        /// Лист «Заказы» — построчная детализация, упорядоченная по дате.
        /// </summary>
        private static void WriteOrdersSheet(
            XLWorkbook workbook,
            IReadOnlyList<OrderHistory> orders,
            ReportSummary summary)
        {
            var ws = workbook.Worksheets.Add("Заказы");

            ws.Cell(1, 1).Value = $"Заказы за период: {FormatPeriod(summary.PeriodStart, summary.PeriodEnd)}";
            ws.Range(1, 1, 1, 6).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 12;

            // Заголовки таблицы.
            const int headerRow = 3;
            ws.Cell(headerRow, 1).Value = "№";
            ws.Cell(headerRow, 2).Value = "Дата";
            ws.Cell(headerRow, 3).Value = "Тип";
            ws.Cell(headerRow, 4).Value = "Клиент";
            ws.Cell(headerRow, 5).Value = "Описание";
            ws.Cell(headerRow, 6).Value = "Сумма (₸)";
            var headerRange = ws.Range(headerRow, 1, headerRow, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Данные — Id показываем как есть (1, 2, 3...).
            int row = headerRow + 1;
            foreach (var order in orders)
            {
                ws.Cell(row, 1).Value = order.Id;
                ws.Cell(row, 2).Value = order.CreatedDate;
                ws.Cell(row, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                ws.Cell(row, 3).Value = NormalizeOperationType(order.OperationType);
                ws.Cell(row, 4).Value = order.ClientName ?? string.Empty;
                ws.Cell(row, 5).Value = order.Description ?? string.Empty;
                ws.Cell(row, 6).Value = order.TotalPrice;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            // Строка «Итого» по выручке — через формулу SUM, чтобы руководство
            // видело: это настоящее Excel-значение, а не захардкоженный total.
            if (orders.Count > 0)
            {
                int firstData = headerRow + 1;
                int lastData = row - 1;
                ws.Cell(row, 1).Value = "Итого";
                ws.Cell(row, 6).FormulaA1 = $"=SUM(F{firstData}:F{lastData})";
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Range(row, 1, row, 6).Style.Font.Bold = true;
                ws.Range(row, 1, row, 6).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(headerRow);
        }

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
