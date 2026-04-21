using SQLite;

namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Станок резки — самостоятельная сущность с собственными экономическими параметрами.
    ///
    /// Зачем вынесено из WorkshopSettings:
    /// 1) в цеху обычно НЕ один станок (несколько лазеров, ленточные пилы,
    ///    пресс-ножницы, гильотина, болгарка) — у каждого своя ставка, мощность
    ///    и амортизация;
    /// 2) сортамент проката (<see cref="RolledProfile"/>) уже знает, на каких
    ///    станках он режется — осталось дать этим станкам реальную стоимость;
    /// 3) даёт возможность задавать фиксированную цену за метр реза
    ///    (<see cref="PricePerMeterOverride"/>) для случаев, когда расчёт по
    ///    формуле уходит в космос или договорённости с клиентом требуют
    ///    простого прейскуранта.
    ///
    /// Интеграция в <c>LaserCostCalculator</c> — отдельный шаг (Спринт 2.2b).
    /// Сейчас эта таблица только хранится и редактируется.
    /// </summary>
    public class CuttingMachine
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Отображаемое имя ("Лазер Bodor C6", "Ленточная пила DoAll").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Тип станка. Хранится как int — stable по номерам.</summary>
        public CuttingMachineKind Kind { get; set; } = CuttingMachineKind.Laser;

        /// <summary>Зарплата оператора в месяц (тг).</summary>
        public decimal OperatorMonthlySalary { get; set; }

        /// <summary>Потребляемая мощность станка (кВт) — для расчёта электроэнергии.</summary>
        public double PowerConsumptionKw { get; set; }

        /// <summary>Амортизация станка (тг/час).</summary>
        public decimal AmortizationPerHour { get; set; }

        /// <summary>Разовая плата за настройку/переналадку на один прогон (тг).</summary>
        public decimal SetupCostPerJob { get; set; }

        /// <summary>Минимальная плата за одно задание (тг). 0 = без минимума.</summary>
        public decimal MinChargePerJob { get; set; }

        /// <summary>
        /// Фиксированная цена за метр реза (тг/м). Если задана (&gt; 0) — перекрывает
        /// вычисленную по формуле цену. NULL/0 — считаем по часовой ставке × скорость.
        /// </summary>
        public decimal? PricePerMeterOverride { get; set; }

        /// <summary>Активен ли станок (используется ли в расчётах и выборе).</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Свободная заметка для цеха — марка, инвентарный номер, особенности.</summary>
        public string Notes { get; set; } = string.Empty;
    }
}
