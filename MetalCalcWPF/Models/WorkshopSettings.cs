using SQLite;

namespace MetalCalcWPF.Models
{
    // Здесь храним ЦЕНЫ и ХАРАКТЕРИСТИКИ ОБОРУДОВАНИЯ
    public class WorkshopSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- 1. Экономика (Цены) ---
        public double ElectricityPricePerKw { get; set; } = 25;    // Цена света (тг/кВт)
        public double OperatorSalaryPerHour { get; set; } = 2000;  // ЗП оператора (тг/час)
        public double OxygenBottlePrice { get; set; } = 3000;      // Цена заправки баллона (тг)
        public double OxygenBottleVolumeM3 { get; set; } = 6.0;    // Объем газа в баллоне (кубы)

        // --- 2. Потребление (Лазер + Чиллер + Приводы) ---
        // Это база, которая жрет ток всегда, когда станок включен
        public double LaserBasePowerConsumption { get; set; } = 15.0; // кВт

        // --- 3. Компрессор (Хитрый!) ---
        // Потребление, когда просто работает аспирация и столы
        public double CompressorIdlePower { get; set; } = 5.0;     // кВт 
        // Потребление, когда режем воздухом (качает давление)
        public double CompressorActivePower { get; set; } = 22.0;  // кВт (винтовые компрессоры жрут много!)

        // Вспомогательный метод: Сколько стоит 1 час работы станка ТОЛЬКО ПО ТОКУ и ЗП (без учета газов)
        public double GetHourlyBaseCost(bool isAirCutting)
        {
            double totalPower;

            if (isAirCutting)
            {
                // Лазер + Компрессор на полную
                totalPower = LaserBasePowerConsumption + CompressorActivePower;
            }
            else
            {
                // Лазер + Компрессор на холостых (аспирация) + (Газ считаем отдельно)
                totalPower = LaserBasePowerConsumption + CompressorIdlePower;
            }

            return OperatorSalaryPerHour + (totalPower * ElectricityPricePerKw);
        }
    }
}