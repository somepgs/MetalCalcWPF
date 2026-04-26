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

        /// <summary>
        /// Заказ с явной cost-разбивкой по 4 операциям. Проверяет, что
        /// <see cref="ReportingService.BuildSummary"/> аккумулирует правильные суммы.
        /// </summary>
        private static OrderHistory CostOrder(
            DateTime date,
            decimal material = 0m, decimal laser = 0m, decimal bending = 0m, decimal welding = 0m,
            decimal total = 0m)
            => new OrderHistory
            {
                CreatedDate = date,
                ClientName = "К",
                Description = "d",
                OperationType = "Металл + Лазер",
                MaterialCost = material,
                LaserCost = laser,
                BendingCost = bending,
                WeldingCost = welding,
                // Если total не задан — считаем как сумму cost-полей.
                TotalPrice = total > 0 ? total : material + laser + bending + welding,
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
        public void BuildSummary_AggregatesCostBreakdown_OrderedByRevenueDescending()
        {
            // С v4 разбивка считается по 4 cost-колонкам (Material/Laser/Bending/Welding),
            // а не по строке OperationType. Один заказ может вносить вклад в несколько групп.
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                // Лазер + Металл (типичный заказ резки)
                CostOrder(Apr01, material: 80_000m, laser: 120_000m),
                // Только лазер (сортамент с известной массой не требует листа)
                CostOrder(Apr01, laser: 100_000m),
                // Гибка + Металл
                CostOrder(Apr01, material: 30_000m, bending: 50_000m),
                // Сварка
                CostOrder(Apr01, welding: 80_000m),
            };

            var summary = svc.BuildSummary(orders, Apr01, May01);

            // Итоги: Лазер 220k > Металл 110k > Сварка 80k > Гибка 50k.
            Assert.AreEqual(4, summary.ByOperation.Count);
            Assert.AreEqual("Лазер",  summary.ByOperation[0].OperationType);
            Assert.AreEqual(220_000m, summary.ByOperation[0].Revenue);
            Assert.AreEqual(2, summary.ByOperation[0].Count, "Два заказа с laser>0");

            Assert.AreEqual("Металл", summary.ByOperation[1].OperationType);
            Assert.AreEqual(110_000m, summary.ByOperation[1].Revenue);

            Assert.AreEqual("Сварка", summary.ByOperation[2].OperationType);
            Assert.AreEqual(80_000m,  summary.ByOperation[2].Revenue);

            Assert.AreEqual("Гибка",  summary.ByOperation[3].OperationType);
            Assert.AreEqual(50_000m,  summary.ByOperation[3].Revenue);

            // Сумма долей по 4 категориям ≈ 1.0, потому что cost-поля точно складываются в TotalPrice.
            var totalShare = summary.ByOperation.Sum(b => b.ShareOfRevenue);
            Assert.AreEqual(1.0, totalShare, 0.0001);
        }

        [TestMethod]
        public void BuildSummary_LegacyOrdersWithoutCostBreakdown_ProduceEmptyByOperation()
        {
            // Заказы, сохранённые до миграции v4 — все cost-поля = 0, есть только TotalPrice.
            // Разбивка по операциям не строится (нечего показывать), но KPI считаются нормально.
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                Order(Apr01, "Laser", 100_000m),
                Order(Apr01, "Bending", 50_000m),
            };

            var summary = svc.BuildSummary(orders, Apr01, May01);

            Assert.AreEqual(2, summary.TotalOrders);
            Assert.AreEqual(150_000m, summary.TotalRevenue);
            Assert.AreEqual(0, summary.ByOperation.Count,
                "Без cost-разбивки ByOperation должна быть пустой — это сигнал, что заказы исторические");
        }

        [TestMethod]
        public void ExportToExcel_OrdersSheet_ContainsApplicationFieldsHeaders()
        {
            // Проверяем, что новые колонки заявки (Этап 3) попадают в Excel:
            // Заявитель, Цех заявителя, Принявший, Срочность, Кол-во, Материал, Масса.
            var svc = new ReportingService();
            var orders = new List<OrderHistory>
            {
                new OrderHistory
                {
                    CreatedDate = new DateTime(2025, 4, 5),
                    ClientName = "К",
                    Description = "x",
                    OperationType = "Металл",
                    TotalPrice = 250_000m,
                    Priority = OrderPriority.Urgent,
                    Quantity = 12,
                    MassKg = 47.3,
                    ApplicantName = "Иванов И.И.",
                    ApplicantWorkshopName = "Цех СВ",
                    AcceptorName = "Петров П.П.",
                    MaterialName = "Сталь Ст3",
                },
            };
            var summary = svc.BuildSummary(orders, Apr01, May01);
            var path = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.xlsx");
            try
            {
                svc.ExportToExcel(orders, summary, path);

                using var wb = new XLWorkbook(path);
                var ws = wb.Worksheet("Заказы");

                // Шапка таблицы — на строке 3.
                var headers = Enumerable.Range(1, 16)
                    .Select(c => ws.Cell(3, c).GetString())
                    .ToList();

                CollectionAssert.Contains(headers, "Заявитель");
                CollectionAssert.Contains(headers, "Цех заявителя");
                CollectionAssert.Contains(headers, "Принявший");
                CollectionAssert.Contains(headers, "Срочность");
                CollectionAssert.Contains(headers, "Кол-во");
                CollectionAssert.Contains(headers, "Материал");
                CollectionAssert.Contains(headers, "Масса (кг)");
                CollectionAssert.Contains(headers, "Дата выполнения");

                // Данные заказа в строке 4.
                Assert.AreEqual("Иванов И.И.", ws.Cell(4, 3).GetString());
                Assert.AreEqual("Цех СВ", ws.Cell(4, 4).GetString());
                Assert.AreEqual("Петров П.П.", ws.Cell(4, 5).GetString());
                Assert.AreEqual("Срочно", ws.Cell(4, 6).GetString(),
                    "OrderPriority.Urgent должен показываться текстом 'Срочно'");
                Assert.AreEqual(12, (int)ws.Cell(4, 7).GetDouble());
                Assert.AreEqual("Сталь Ст3", ws.Cell(4, 8).GetString());
                Assert.AreEqual(47.3, ws.Cell(4, 10).GetDouble(), 0.01);
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
            }
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
