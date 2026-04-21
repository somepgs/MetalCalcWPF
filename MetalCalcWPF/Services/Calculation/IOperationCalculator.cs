using MetalCalcWPF.Models;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Контракт одной операционной калькуляции (металл / лазер / гибка / сварка).
    ///
    /// Каждая реализация:
    /// 1) решает, применима ли она к этому запросу (смотрит флаги/размеры);
    /// 2) если да — вычисляет свою часть стоимости и записывает её
    ///    в соответствующее поле <see cref="CalculationResult"/>
    ///    (MaterialCost / LaserCost / BendingCost / WeldingCost),
    ///    плюс, при необходимости, детализацию (LaserDetails / WeldingDetails);
    /// 3) дописывает короткий фрагмент в <see cref="CalculationResult.Log"/>
    ///    через метод <see cref="AppendLog"/> у оркестратора (см. CalculationService).
    ///
    /// ВАЖНО: калькулятор НЕ должен переписывать поля чужих операций —
    /// пишет только «своё». Это держит операции независимыми и тестируемыми по отдельности.
    /// </summary>
    public interface IOperationCalculator
    {
        /// <summary>
        /// Короткое имя операции для логов (например, "Material", "Laser").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Применить калькулятор к запросу. Метод ДОЛЖЕН быть идемпотентным
        /// относительно одного и того же <paramref name="request"/> — ничего
        /// не кешировать во внутреннем состоянии между вызовами.
        /// </summary>
        /// <returns>
        /// Короткий фрагмент для сводного лога (например,
        /// "+ Laser(5x pierce)"). Пустая строка — если операция не применялась.
        /// </returns>
        string Apply(CalculationRequest request, WorkshopSettings settings, CalculationResult result);
    }
}
