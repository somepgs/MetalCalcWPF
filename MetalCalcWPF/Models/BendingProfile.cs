using SQLite;

namespace MetalCalcWPF.Models
{
    public class BendingProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public double Thickness { get; set; }  // Толщина металла (мм)
        public double V_Die { get; set; }      // Размер матрицы (V15, V24...)
        public double MinFlange { get; set; }  // Минимальная полка (b) из таблицы

        public double PricePerBend { get; set; } // Цена за 1 гиб для этой толщины
    }
}