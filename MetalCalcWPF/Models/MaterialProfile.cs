using SQLite;

namespace MetalCalcWPF.Models
{
    public class MaterialProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public double Thickness { get; set; }    // Толщина (мм) - Столбец A
        public string GasType { get; set; }      // Газ (Воздух/Кислород/Азот) - Столбец B
        public double CuttingSpeed { get; set; } // Скорость (м/мин) - Столбец C

        // В Excel у тебя цена пробивки задана вручную (10, 20... тенге)
        public double PiercePrice { get; set; }  // Цена за 1 пробивку - Столбец E

        // Наценка меняется от 150% до 35%
        public double MarkupCoefficient { get; set; } // Коэффициент наценки - Столбец H
    }
}