using MetalCalcWPF.Models;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Неизменяемый пакет входных параметров одной калькуляции заказа.
    /// Используется вместо «простыни» аргументов у CalculateOrder, чтобы:
    /// 1) параметры не путались местами при рефакторинге;
    /// 2) операционные калькуляторы получали единый объект;
    /// 3) можно было легко добавлять поля, не ломая сигнатуру.
    /// </summary>
    public class CalculationRequest
    {
        public double WidthMm { get; init; }
        public double HeightMm { get; init; }
        public double ThicknessMm { get; init; }
        public int Quantity { get; init; }

        public MaterialType? Material { get; init; }

        public double LaserLengthMeters { get; init; }
        public int PiercesCount { get; init; }

        public bool UseBending { get; init; }
        public int BendsCount { get; init; }
        public double BendLengthMm { get; init; }

        public bool UseWelding { get; init; }
        public double WeldLengthCm { get; init; }

        /// <summary>
        /// Если &gt; 0 — используется как ИСТИННАЯ масса всей партии (кг),
        /// минуя расчёт по объёму. Применяется для режима «Сортамент проката»
        /// (длина × кг/м × кол-во).
        /// </summary>
        public double MeasuredWeightKg { get; init; }
    }
}
