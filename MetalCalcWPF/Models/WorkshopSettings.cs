using SQLite;

namespace MetalCalcWPF.Models
{
    public class WorkshopSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- 1. ОБЩИЕ и ЛАЗЕР ---
        public double ElectricityPricePerKw { get; set; } = 25;

        // Оператор ЛАЗЕРА
        public double OperatorMonthlySalary { get; set; } = 300000;
        public int WorkDaysPerMonth { get; set; } = 26;
        public int WorkHoursPerDay { get; set; } = 8;

        public double OxygenBottlePrice { get; set; } = 5000; // ✅ ОБНОВЛЕНО: 5000 тенге
        public double AmortizationPerHour { get; set; } = 650;

        // Дополнительные настройки для лазера
        public double LaserSetupCostPerJob { get; set; } = 1000; // Наладка за партию
        public double LaserMinChargePerJob { get; set; } = 500;   // Минимальная стоимость резки за заказ
        public double PierceTimeSeconds { get; set; } = 5;        // Время на 1 пробивку (сек)

        // ✅ НОВЫЕ ПАРАМЕТРЫ КИСЛОРОДА
        public double OxygenBottleVolumeLiters { get; set; } = 40;    // Объем баллона (литры)
        public double OxygenBottlePressureAtm { get; set; } = 150;    // Давление (атмосферы)
        public double OxygenFlowRateLpm { get; set; } = 15;           // Расход (л/мин)

        // Сложность (Кувалда)
        public double HeavyMaterialThresholdMm { get; set; } = 20.0;
        public double HeavyHandlingCostPerDetail { get; set; } = 500;

        // Мощность Лазера
        public double LaserBasePowerConsumption { get; set; } = 28.0;
        public double CompressorIdlePower { get; set; } = 5.0;
        public double CompressorActivePower { get; set; } = 22.0;

        // --- 2. ЛИСТОГИБ (Bodor 600T) ---
        public double BendingOperatorSalary { get; set; } = 450000;
        public double BendingMachinePower { get; set; } = 45.0;
        public double MaxBendingLengthMm { get; set; } = 6000;

        public double BendingSetupPrice { get; set; } = 1000; // Резерв
        public double BendingBasePrice { get; set; } = 50;    // Резерв

        // --- 3. СВАРКА ---
        public double WeldingCostPerCm { get; set; } = 20;

        // --- 4. МАТЕРИАЛЫ ---
        public double MaterialMarkupPercent { get; set; } = 30.0;

        // --- МЕТОДЫ РАСЧЕТА ---

        // Стоимость часа ЛАЗЕРА
        public double GetHourlyBaseCost(bool isAirCutting)
        {
            double totalHours = WorkDaysPerMonth * WorkHoursPerDay;
            if (totalHours == 0) totalHours = 1;
            double salaryPerHour = OperatorMonthlySalary / totalHours;

            double totalPower = isAirCutting
                ? (LaserBasePowerConsumption + CompressorActivePower)
                : (LaserBasePowerConsumption + CompressorIdlePower);

            return salaryPerHour + (totalPower * ElectricityPricePerKw) + AmortizationPerHour;
        }

        // Стоимость часа ЛИСТОГИБА
        public double GetBendingHourlyCost()
        {
            double totalHours = WorkDaysPerMonth * WorkHoursPerDay;
            if (totalHours == 0) totalHours = 1;

            double salaryPerHour = BendingOperatorSalary / totalHours;
            double powerCost = BendingMachinePower * ElectricityPricePerKw;

            return salaryPerHour + powerCost + AmortizationPerHour;
        }

        // ✅ НОВЫЙ МЕТОД: Расчет стоимости кислорода за минуту работы
        public double GetOxygenCostPerMinute()
        {
            // Общий объем кислорода в баллоне (литры при атмосферном давлении)
            double totalOxygenLiters = OxygenBottleVolumeLiters * OxygenBottlePressureAtm;

            // Время работы одного баллона (минуты)
            double bottleWorkTimeMinutes = totalOxygenLiters / OxygenFlowRateLpm;

            // Цена за минуту
            return OxygenBottlePrice / bottleWorkTimeMinutes;
        }
    }
}