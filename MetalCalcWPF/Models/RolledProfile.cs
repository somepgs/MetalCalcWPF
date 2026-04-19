using SQLite;

namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Запись сортамента проката: уголок, швеллер, двутавр, профтруба, лист и т.п.
    /// Ключевая характеристика — масса на 1 погонный метр (кг/м), по которой считается вес заказа.
    /// </summary>
    public class RolledProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Тип сортамента (форма).</summary>
        public ProfileKind Kind { get; set; }

        /// <summary>
        /// Короткий код типоразмера для поиска и отображения.
        /// Примеры: "50x50x5", "№10", "80x40x3", "Лист 3".
        /// </summary>
        public string SizeCode { get; set; } = string.Empty;

        /// <summary>
        /// Полное ГОСТ-обозначение для печати в КП и спецификациях.
        /// Пример: "Уголок 50×50×5 ГОСТ 8509-93 / Ст3 ГОСТ 535-2005".
        /// </summary>
        public string GostDesignation { get; set; } = string.Empty;

        /// <summary>Масса 1 погонного метра, кг/м. Основа расчёта.</summary>
        public double WeightPerMeterKg { get; set; }

        // ------------------- Геометрия (для справки/фильтрации) -------------------

        /// <summary>Высота/большая сторона/номер профиля, мм.</summary>
        public double? Height { get; set; }

        /// <summary>Ширина/меньшая сторона, мм.</summary>
        public double? Width { get; set; }

        /// <summary>Толщина стенки или полки, мм.</summary>
        public double? WallThickness { get; set; }

        /// <summary>Толщина полки (для двутавров/швеллеров), мм.</summary>
        public double? FlangeThickness { get; set; }

        /// <summary>Наружный диаметр (для круглых труб/кругов), мм.</summary>
        public double? OuterDiameter { get; set; }

        // ------------------- Коммерческая часть -------------------

        /// <summary>
        /// Марка стали (FK на MaterialType). Если задана — цена = масса × BasePricePerKg × наценка.
        /// Можно оставить null, если используется PricePerMeterOverride.
        /// </summary>
        public int? MaterialTypeId { get; set; }

        /// <summary>
        /// Прямая цена за 1 погонный метр (закупочная, без наценки).
        /// Если задана — она используется вместо расчёта через массу и MaterialType.
        /// </summary>
        public decimal? PricePerMeterOverride { get; set; }

        // ------------------- Совместимость со станками резки -------------------

        /// <summary>
        /// Какие станки могут резать этот профиль. Битовая маска <see cref="CuttingMachines"/>.
        /// </summary>
        public int CompatibleMachines { get; set; }

        /// <summary>Признак «снят с производства / не закупаем».</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Пользовательский комментарий.</summary>
        public string? Notes { get; set; }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(GostDesignation) ? $"{Kind} {SizeCode}" : GostDesignation;
    }
}
