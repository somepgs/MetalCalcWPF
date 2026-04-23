using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты отчётности (Спринт 2.3).
    ///
    /// <para>BuildSummary — чистая агрегация, проверяется на фейковых данных.</para>
    /// <para>ExportToExcel — I/O, но ClosedXML умеет открыть любой xlsx обратно,
    /// поэтому читаем получившийся файл и убеждаемся, что листы и ключевые
    /// ячейки на месте. Это защищает от поломки формата после рефакторинга.</para>
    /// </summary>
    [TestClass]
    public class ReportingServiceTests
    {
        private static readonly DateTime Apr01 = new DateTime(2025, 4, 1);
        private static readonly DateTime May01 = new DateTime(2025, 5, 1);

        private static OrderHistory Order(DateTime date, string op, decimal price)
            => new OrderHistory
            {
                CreatedDate = date,
                ClientName = "К",
                Description = "d",
                OperationType = op,
                TotalPrice = price,
            };

        [TestMethod]
        public void BuildSummary_EmptyPeriod_ReturnsZeros()
        {
            var svc = new ReportingService();
            var summary = svc.BuildSummary(new List<OrderHistory>(), Apr01, May01);

            Assert.AreEqual(0, summary.TotalOrders);
            Assert.AreEqual(0m, summary.TotalRevenue);
            Assert.AreEqual(0m, summary.AverageOrderValue,
                "Средний чек на пустом периоде не должен делить на ноль");
            Assert.AreEqual(0, summary.ByOperation.Count);
            Assert.AreEqual(Apr01, summary.PeriodStart);
            Assert.AreEqual(May01, summary.PeriodEnd);
        }

        [TestMethod]
        public void BuildSummary_AggregatesRevenueAndAverage()
        {
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                Order(new DateTime(2025, 4, 5),  "Laser", 100_000m),
                Order(new DateTime(2025, 4, 10), "Laser", 200_000m),
                Order(new DateTime(2025, 4, 15), "Bending", 50_000m),
            };

            var summary = svc.BuildSummary(orders, Apr01, May01);

            Assert.AreEqual(3, summary.TotalOrders);
            Assert.AreEqual(350_000m, summary.TotalRevenue);
            // 350 000 / 3 ≈ 116 666.67 — точное decimal-деление без округления.
            Assert.AreEqual(350_000m / 3m, summary.AverageOrderValue);
        }

        [TestMethod]
        public void BuildSummary_GroupsByOperation_OrderedByRevenueDescending()
        {
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                Order(Apr01, "Bending", 50_000m),
                Order(Apr01, "Laser", 200_000m),
                Order(Apr01, "Laser", 100_000m),
                Order(Apr01, "Welding", 80_000m),
            };

            var summary = svc.BuildSummary(orders, Apr01, May01);

            Assert.AreEqual(3, summary.ByOperation.Count);

            // Laser = 300k → самый денежный → идёт первым.
            Assert.AreEqual("Laser", summary.ByOperation[0].OperationType);
            Assert.AreEqual(300_000m, summary.ByOperation[0].Revenue);
            Assert.AreEqual(2, summary.ByOperation[0].Count);

            // Welding = 80k → второй.
            Assert.AreEqual("Welding", summary.ByOperation[1].OperationType);

            // Bending = 50k → последний.
            Assert.AreEqual("Bending", summary.ByOperation[2].OperationType);

            // Доли в сумме ≈ 1.0 (с плавающей точностью).
            var totalShare = summary.ByOperation.Sum(b => b.ShareOfRevenue);
            Assert.AreEqual(1.0, totalShare, 0.0001);
        }

        [TestMethod]
        public void BuildSummary_MultilineOperationType_UsesFirstLineAsShortName()
        {
            // Сейчас MainViewModel пишет в OperationType весь result.Log (многострочный).
            // Агрегация должна взять первую строку как короткое имя операции.
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                Order(Apr01, "Laser\nПодробности: сталь 3мм × 10м\nгаз: O2", 100m),
                Order(Apr01, "Laser\nПодробности: сталь 5мм × 5м\nгаз: N2",  200m),
            };

            var summary = svc.BuildSummary(orders, Apr01, May01);

            Assert.AreEqual(1, summary.ByOperation.Count,
                "Обе записи должны попасть в одну группу 'Laser'");
            Assert.AreEqual("Laser", summary.ByOperation[0].OperationType);
            Assert.AreEqual(300m, summary.ByOperation[0].Revenue);
        }

        [TestMethod]
        public void ExportToExcel_WritesBothSheetsWithKeyCells()
        {
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                Order(new DateTime(2025, 4, 5),  "Laser",   100_000m),
                Order(new DateTime(2025, 4, 15), "Bending", 50_000m),
            };
            var summary = svc.BuildSummary(orders, Apr01, May01);

            var path = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.xlsx");
            try
            {
                svc.ExportToExcel(orders, summary, path);

                Assert.IsTrue(File.Exists(path), "Файл отчёта должен быть создан");

                using var wb = new XLWorkbook(path);
                Assert.IsTrue(wb.TryGetWorksheet("Итоги", out _), "Должен быть лист 'Итоги'");
                Assert.IsTrue(wb.TryGetWorksheet("Заказы", out _), "Должен быть лист 'Заказы'");

                var itogi = wb.Worksheet("Итоги");
                Assert.AreEqual("Отчёт по заказам", itogi.Cell(1, 1).GetString(),
                    "В A1 ожидается заголовок отчёта");

                // KPI «Всего заказов» — 2.
                var allCells = itogi.CellsUsed().ToList();
                var totalOrdersLabel = allCells.FirstOrDefault(c => c.GetString() == "Всего заказов");
                Assert.IsNotNull(totalOrdersLabel, "Ожидается ячейка 'Всего заказов'");
                Assert.AreEqual(2d, totalOrdersLabel.CellRight().GetDouble());

                var zakazy = wb.Worksheet("Заказы");
                // Заголовок второго листа — содержит подстроку "Заказы за период".
                Assert.IsTrue(
                    zakazy.Cell(1, 1).GetString().Contains("Заказы за период"),
                    "Шапка листа 'Заказы' должна содержать период");
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
            }
        }
    }
}
