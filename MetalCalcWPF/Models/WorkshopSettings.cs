using SQLite;

namespace MetalCalcWPF.Models
{
    public class WorkshopSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- 1. ОБЩИЕ и ЛАЗЕР ---
        public decimal ElectricityPricePerKw { get; set; } = 38m; // ✅ Обновлено из Excel

        // Оператор ЛАЗЕРА
        public decimal OperatorMonthlySalary { get; set; } = 500000m; // ✅ Обновлено
        public int WorkDaysPerMonth { get; set; } = 25; // ✅ Обновлено (25 смен)
        public int WorkHoursPerDay { get; set; } = 9;   // ✅ Обновлено (9 часов)

        public decimal OxygenBottlePrice { get; set; } = 5000m;
        public decimal AmortizationPerHour { get; set; } = 650m;

        // Дополнительные настройки для лазера
        public decimal LaserSetupCostPerJob { get; set; } = 1000m;
        public decimal LaserMinChargePerJob { get; set; } = 500m;
        public double PierceTimeSeconds { get; set; } = 5;

        // Параметры кислорода
        public double OxygenBottleVolumeLiters { get; set; } = 40;
        public double OxygenBottlePressureAtm { get; set; } = 150;
        public double OxygenFlowRateLpm { get; set; } = 15;

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

        public decimal BendingSetupPrice { get; set; } = 1000m;
        public decimal BendingBasePrice { get; set; } = 50m;

        // --- 3. СВАРКА (Полуавтомат MIG/MAG, проволока 1.2мм, CO2) ---
        
        /// <summary>
        /// Зарплата сварщика (тг/мес)
        /// </summary>
        public decimal WelderMonthlySalary { get; set; } = 450000m;

        /// <summary>
        /// Цена сварочной проволоки (тг/кг)
        /// </summary>
        public decimal WeldingWirePricePerKg { get; set; } = 1530m;

        /// <summary>
        /// Расход проволоки (г/см)
        /// </summary>
        public double WeldingWireConsumptionGPerCm { get; set; } = 5.0;

        /// <summary>
        /// Стоимость баллона газа для сварки (тг)
        /// </summary>
        public decimal WeldingGasBottlePrice { get; set; } = 15000m;

        /// <summary>
        /// Объем баллона газа (литры)
        /// </summary>
        public double WeldingGasBottleVolumeLiters { get; set; } = 40;

        /// <summary>
        /// Давление баллона (атм)
        /// </summary>
        public double WeldingGasBottlePressureAtm { get; set; } = 150;

        /// <summary>
        /// Расход газа (л/мин)
        /// </summary>
        public double WeldingGasFlowLpm { get; set; } = 15.0;

        /// <summary>
        /// Бюджет на расходники (стекла, сопла) в месяц (тг)
        /// </summary>
        public decimal WeldingConsumablesBudget { get; set; } = 8000m;

        /// <summary>
        /// Коэффициент наценки на сварку
        /// </summary>
        public double WeldingMarkupCoefficient { get; set; } = 3.0;

        // Устаревший параметр (оставлен для обратной совместимости)
        public decimal WeldingCostPerCm { get; set; } = 20m;

        // Метод для расчёта стоимости газа для сварки (тг/мин)
        public decimal GetWeldingGasCostPerMinute()
        {
            double totalLiters = WeldingGasBottleVolumeLiters * WeldingGasBottlePressureAtm;
            if (WeldingGasFlowLpm <= 0 || totalLiters <= 0) return 0m;
            double minutes = totalLiters / WeldingGasFlowLpm;
            if (minutes <= 0) return 0m;
            return WeldingGasBottlePrice / (decimal)minutes;
        }

        // --- 4. МАТЕРИАЛЫ ---
        public decimal MaterialMarkupPercent { get; set; } = 30.0m;

        // --- МЕТОДЫ РАСЧЕТА ---

        /// <summary>
        /// Стоимость часа ЛАЗЕРА
        /// </summary>
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

        /// <summary>
        /// Стоимость часа ЛИСТОГИБА
        /// </summary>
        public decimal GetBendingHourlyCost()
        {
            double totalHours = WorkDaysPerMonth * WorkHoursPerDay;
            if (totalHours == 0) totalHours = 1;
            decimal salaryPerHour = BendingOperatorSalary / (decimal)totalHours;
            decimal powerCost = (decimal)BendingMachinePower * ElectricityPricePerKw;

            return salaryPerHour + powerCost + AmortizationPerHour;
        }

        /// <summary>
        /// Расчет стоимости кислорода за минуту работы
        /// </summary>
        public decimal GetOxygenCostPerMinute()
        {
            double totalOxygenLiters = OxygenBottleVolumeLiters * OxygenBottlePressureAtm;
            if (OxygenFlowRateLpm <= 0 || totalOxygenLiters <= 0)
                return 0.0m;

            double bottleWorkTimeMinutes = totalOxygenLiters / OxygenFlowRateLpm;
            if (double.IsInfinity(bottleWorkTimeMinutes) || bottleWorkTimeMinutes <= 0)
                return 0.0m;

            return OxygenBottlePrice / (decimal)bottleWorkTimeMinutes;
        }

        /// <summary>
        /// ✅ НОВЫЙ МЕТОД: Расчет стоимости 1 минуты сварки
        /// Включает: ЗП сварщика + CO2 газ + расходники
        /// </summary>
        public decimal GetWeldingCostPerMinute()
        {
            // Общее количество рабочих минут в месяц
            double totalMinutes = WorkDaysPerMonth * WorkHoursPerDay * 60;
            if (totalMinutes == 0) totalMinutes = 1;

            // ЗП сварщика за минуту
            decimal salaryPerMinute = WelderMonthlySalary / (decimal)totalMinutes;

            // Стоимость газа за минуту
            decimal gasPerMinute = GetWeldingGasCostPerMinute();

            // Расходники (стекла, сопла) за минуту
            decimal consumablesPerMinute = WeldingConsumablesBudget / (decimal)totalMinutes;

            return salaryPerMinute + gasPerMinute + consumablesPerMinute;
        }
    }
}
