using SQLite;

namespace MetalCalcWPF.Models
{
    public class MaterialType
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }      // "Черная сталь (Ст3)", "Нержавейка"
        public double Density { get; set; }   // Плотность (г/см3). Для стали 7.85
        public decimal BasePricePerKg { get; set; } // Базовая цена (если фиксированная)

        // Переопределение метода ToString, чтобы в выпадающем списке писалось название
        public override string ToString()
        {
            return Name;
        }
    }
}