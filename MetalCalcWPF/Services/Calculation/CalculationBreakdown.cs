using System.Collections.Generic;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Одна строка «прозрачной» детализации расчёта — шаг формулы, который
    /// пользователь видит в UI и может сверить с Excel-моделью.
    ///
    /// <para>
    /// <see cref="Label"/> — человекочитаемое название величины
    /// ("Цена минуты лазера", "Себестоимость метра", "Итого по лазеру").
    /// </para>
    /// <para>
    /// <see cref="Formula"/> — как именно получили значение
    /// ("85 тг/мин × 0.714 мин/м", "1 / 1.4", "60.71 × 40"). Это главный
    /// инструмент прозрачности: смотря на эту строку, оператор сразу видит,
    /// что подставилось и откуда пришло число.
    /// </para>
    /// <para>
    /// <see cref="Value"/> — итоговое численное значение шага (в тг, если не
    /// оговорено иначе в <see cref="Unit"/>).
    /// </para>
    /// <para>
    /// <see cref="Unit"/> — единица измерения ("тг", "тг/м", "мин/м", "м/мин").
    /// Нужна, потому что не все строки — деньги.
    /// </para>
    /// <para>
    /// <see cref="IsTotal"/> — это итоговая строка секции (выделяется жирным в UI).
    /// </para>
    /// </summary>
    public record BreakdownLine(string Label, string Formula, decimal Value, string Unit = "тг", bool IsTotal = false);

    /// <summary>
    /// Детализация одной операции в составе расчёта (Металл / Лазер / Гибка / Сварка).
    ///
    /// Порядок <see cref="Lines"/> имеет значение — это та самая цепочка
    /// «как мы сюда пришли», которую пользователь читает сверху вниз.
    ///
    /// Собирается калькулятором по ходу <c>Apply</c> и попадает в
    /// <see cref="CalculationResult.Breakdowns"/>. UI рисует каждую секцию
    /// отдельным Expander'ом.
    /// </summary>
    public class CalculationBreakdown
    {
        /// <summary>
        /// Название секции, как его увидит пользователь ("Лазер", "Сварка").
        /// </summary>
        public string Section { get; init; } = string.Empty;

        /// <summary>
        /// Короткий подзаголовок с ключевыми входными параметрами
        /// ("16мм / кислород / 1.4 м/мин / K=40"). Помогает понять, какие
        /// исходные данные применялись, без раскрытия всей таблицы.
        /// </summary>
        public string Subtitle { get; init; } = string.Empty;

        /// <summary>
        /// Шаги расчёта в порядке применения. Последний шаг обычно помечен
        /// <see cref="BreakdownLine.IsTotal"/> = true.
        /// </summary>
        public List<BreakdownLine> Lines { get; } = new();
    }
}
