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

        public double OxygenBottlePrice { get; set; } = 3000;
        public double AmortizationPerHour { get; set; } = 650;

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

        // --- 3. СВАРКА (Вот это поле пропало, я его вернул!) ---
        public double WeldingCostPerCm { get; set; } = 20;

        // --- 4. МАТЕРИАЛЫ ---
        // Наценка на металл в процентах (чтобы перекрыть обрезки и доставку)
        // Если купили за 355, а наценка 30%, то продаем за 461 тг
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
    }
}