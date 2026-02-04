using SQLite;

namespace MetalCalcWPF.Models
{
    public class BendingProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public double Thickness { get; set; }
        public double V_Die { get; set; }
        public double MinFlange { get; set; }

        // --- НОВЫЕ ЦЕНЫ (по длине гиба) ---
        public double PriceLen1500 { get; set; } // Цена до 1.5м (Столбец C)
        public double PriceLen3000 { get; set; } // Цена 1.5м - 3м (Столбец D)
        public double PriceLen6000 { get; set; } // Цена 3м - 6м (Столбец E)

        public double SetupPrice { get; set; }   // Цена наладки (Столбец F)
    }
}