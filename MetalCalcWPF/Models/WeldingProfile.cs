using SQLite;

namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Профиль сварки для разных катетов шва
    /// Основан на данных из Excel справочника
    /// </summary>
    public class WeldingProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Катет шва (мм) - определяется толщиной свариваемого металла
        /// Обычно: Катет = 0.7 × Толщина металла
        /// </summary>
        public double FilletSize { get; set; }

        /// <summary>
        /// Скорость сварки (см/мин)
        /// Чем толще металл, тем медленнее сварка
        /// </summary>
        public double WeldingSpeed { get; set; }

        /// <summary>
        /// Вес шва на 1 см длины (г/см)
        /// Используется для расчета расхода проволоки
        /// </summary>
        public double WeightPerCm { get; set; }

        /// <summary>
        /// Себестоимость 1 см шва (тг/см)
        /// Проволока + Работа + Газ + Расходники
        /// </summary>
        public decimal CostPerCm { get; set; }

        /// <summary>
        /// Цена для клиента за 1 см шва (тг/см)
        /// Себестоимость × Коэффициент наценки
        /// </summary>
        public decimal PricePerCm { get; set; }

        /// <summary>
        /// Коэффициент наценки (обычно 3.0)
        /// </summary>
        public double MarkupCoefficient { get; set; } = 3.0;
    }
}
