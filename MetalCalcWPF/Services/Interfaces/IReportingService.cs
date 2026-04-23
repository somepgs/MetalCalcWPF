using System;
using System.Collections.Generic;
using MetalCalcWPF.Models;
using MetalCalcWPF.Models.Reporting;

namespace MetalCalcWPF.Services.Interfaces
{
    /// <summary>
    /// Сервис отчётности для руководства (Спринт 2.3).
    ///
    /// Два уровня:
    /// 1) <see cref="BuildSummary"/> — чистая функция агрегации, без I/O,
    ///    удобно тестировать на фейковых наборах заказов;
    /// 2) <see cref="ExportToExcel"/> — I/O-операция, кладёт на диск
    ///    xlsx-файл с шапкой, детализацией и листом «Итоги».
    ///
    /// Разделено специально, чтобы UI мог показать агрегаты над таблицей
    /// (средний чек, разбивка по операциям) без создания Excel-файла.
    /// </summary>
    public interface IReportingService
    {
        /// <summary>
        /// Считает агрегаты по переданному списку заказов за указанный период.
        /// Заказы вне <paramref name="periodStart"/>..<paramref name="periodEnd"/>
        /// всё равно попадут в сумму — диапазон используется только как метка
        /// в возвращаемом <see cref="ReportSummary"/>. Фильтрацию делает
        /// вызывающая сторона через <see cref="IDatabaseService.GetOrdersByDateRange"/>.
        /// </summary>
        ReportSummary BuildSummary(
            IReadOnlyList<OrderHistory> orders,
            DateTime periodStart,
            DateTime periodEnd);

        /// <summary>
        /// Сохраняет профессиональный отчёт в xlsx по указанному пути.
        /// Файл содержит:
        /// - лист «Заказы» с шапкой-периодом и детализацией;
        /// - лист «Итоги» с агрегатами и разбивкой по операциям.
        /// </summary>
        /// <exception cref="System.IO.IOException">если файл занят другим процессом.</exception>
        void ExportToExcel(
            IReadOnlyList<OrderHistory> orders,
            ReportSummary summary,
            string filePath);
    }
}
