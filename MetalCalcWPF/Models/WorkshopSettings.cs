using SQLite;

namespace MetalCalcWPF.Models
{
    public class WorkshopSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- 1. Экономика и Зарплата ---
        public double ElectricityPricePerKw { get; set; } = 25;

        // Новые поля для зарплаты
        public double OperatorMonthlySalary { get; set; } = 300000; // Оклад (тг)
        public int WorkDaysPerMonth { get; set; } = 26;             // График 6/1 (~26 дней)
        public int WorkHoursPerDay { get; set; } = 8;               // Смена 8 часов

        // --- 2. Расходники ---
        public double OxygenBottlePrice { get; set; } = 3000;
        public double AmortizationPerHour { get; set; } = 650;

        // --- 3. Сложность (Кувалда и Кран-балка) ---
        // Начиная с какой толщины считаем деталь "Тяжелой"?
        public double HeavyMaterialThresholdMm { get; set; } = 20.0; // Было 10, ставим 20

        // Сколько добавляем к стоимости за сложность (в тенге за деталь)?
        // Это плата за то, что оператор машет кувалдой и краном
        public double HeavyHandlingCostPerDetail { get; set; } = 500;

        // --- 4. Оборудование (50 кВт) ---
        public double LaserBasePowerConsumption { get; set; } = 28.0;
        public double CompressorIdlePower { get; set; } = 5.0;
        public double CompressorActivePower { get; set; } = 22.0;

        // --- УМНЫЙ РАСЧЕТ СТОИМОСТИ ЧАСА ---
        public double GetHourlyBaseCost(bool isAirCutting)
        {
            // 1. Считаем реальную ставку в час: Оклад / (Дни * Часы)
            double totalHoursInMonth = WorkDaysPerMonth * WorkHoursPerDay;
            // Защита от деления на ноль
            if (totalHoursInMonth == 0) totalHoursInMonth = 1;

            double realHourlySalary = OperatorMonthlySalary / totalHoursInMonth;

            // 2. Считаем потребление тока
            double totalPower;
            if (isAirCutting)
                totalPower = LaserBasePowerConsumption + CompressorActivePower;
            else
                totalPower = LaserBasePowerConsumption + CompressorIdlePower;

            // Итого: ЗП + Свет + Амортизация
            return realHourlySalary + (totalPower * ElectricityPricePerKw) + AmortizationPerHour;
        }
    }
}