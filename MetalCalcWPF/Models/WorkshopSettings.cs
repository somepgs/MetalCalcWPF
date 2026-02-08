using SQLite;

namespace MetalCalcWPF.Models
{
    public class WorkshopSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- 1. ОБЩИЕ и ЛАЗЕР ---
        public decimal ElectricityPricePerKw { get; set; } = 25m;

        // Оператор ЛАЗЕРА
        public decimal OperatorMonthlySalary { get; set; } = 300000m;
        public int WorkDaysPerMonth { get; set; } = 26;
        public int WorkHoursPerDay { get; set; } = 8;

        public decimal OxygenBottlePrice { get; set; } = 5000m; // ✅ ОБНОВЛЕНО: 5000 тенге
        public decimal AmortizationPerHour { get; set; } = 650m;

        // Дополнительные настройки для лазера
        public decimal LaserSetupCostPerJob { get; set; } = 1000m; // Наладка за партию
        public decimal LaserMinChargePerJob { get; set; } = 500m;   // Минимальная стоимость резки за заказ
        public double PierceTimeSeconds { get; set; } = 5;        // Время на 1 пробивку (сек)

        // ✅ НОВЫЕ ПАРАМЕТРЫ КИСЛОРОДА
        public double OxygenBottleVolumeLiters { get; set; } = 40;    // Объем баллона (литры)
        public double OxygenBottlePressureAtm { get; set; } = 150;    // Давление (атмосферы)
        public double OxygenFlowRateLpm { get; set; } = 15;           // Расход (л/мин)

        // Сложность (Кувалда)
        public double HeavyMaterialThresholdMm { get; set; } = 20.0;
        public decimal HeavyHandlingCostPerDetail { get; set; } = 500m;

        // Мощность Лазера
        public double LaserBasePowerConsumption { get; set; } = 28.0;
        public double CompressorIdlePower { get; set; } = 5.0;
        public double CompressorActivePower { get; set; } = 22.0;

        // --- 2. ЛИСТОГИБ (Bodor 600T) ---
        public decimal BendingOperatorSalary { get; set; } = 450000m;
        public double BendingMachinePower { get; set; } = 45.0;
        public double MaxBendingLengthMm { get; set; } = 6000;

        public decimal BendingSetupPrice { get; set; } = 1000m; // Резерв
        public decimal BendingBasePrice { get; set; } = 50m;    // Резерв

        // --- 3. СВАРКА ---
        public decimal WeldingCostPerCm { get; set; } = 20m;

        // --- 4. МАТЕРИАЛЫ ---
        public decimal MaterialMarkupPercent { get; set; } = 30.0m;

        // --- МЕТОДЫ РАСЧЕТА ---

        // Стоимость часа ЛАЗЕРА
        public decimal GetHourlyBaseCost(bool isAirCutting)
        {
            double totalHours = WorkDaysPerMonth * WorkHoursPerDay;
            if (totalHours == 0) totalHours = 1;
            decimal salaryPerHour = OperatorMonthlySalary / (decimal)totalHours;

            double totalPower = isAirCutting
                ? (LaserBasePowerConsumption + CompressorActivePower)
                : (LaserBasePowerConsumption + CompressorIdlePower);

            decimal totalPowerDec = (decimal)totalPower;

            return salaryPerHour + (totalPowerDec * ElectricityPricePerKw) + AmortizationPerHour;
        }

        // Стоимость часа ЛИСТОГИБА
        public decimal GetBendingHourlyCost()
        {
            double totalHours = WorkDaysPerMonth * WorkHoursPerDay;
            if (totalHours == 0) totalHours = 1;
            decimal salaryPerHour = BendingOperatorSalary / (decimal)totalHours;
            decimal powerCost = (decimal)BendingMachinePower * ElectricityPricePerKw;

            return salaryPerHour + powerCost + AmortizationPerHour;
        }

        // ✅ НОВЫЙ МЕТОД: Расчет стоимости кислорода за минуту работы
        public decimal GetOxygenCostPerMinute()
        {
            // Общий объем кислорода в баллоне (литры при атмосферном давлении)
            double totalOxygenLiters = OxygenBottleVolumeLiters * OxygenBottlePressureAtm;
            // Защита от некорректных значений
            if (OxygenFlowRateLpm <= 0 || totalOxygenLiters <= 0)
            {
                return 0.0m;
            }

            // Время работы одного баллона (минуты)
            double bottleWorkTimeMinutes = totalOxygenLiters / OxygenFlowRateLpm;

            if (double.IsInfinity(bottleWorkTimeMinutes) || bottleWorkTimeMinutes <= 0)
                return 0.0m;

            // Цена за минуту
            var costPerMinute = OxygenBottlePrice / (decimal)bottleWorkTimeMinutes;
            return costPerMinute;
        }
    }
}