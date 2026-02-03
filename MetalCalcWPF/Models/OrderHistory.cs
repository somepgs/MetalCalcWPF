using SQLite;
using System;

namespace MetalCalcWPF.Models
{
    public class OrderHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; } // Уникальный номер заказа (1, 2, 3...)

        public DateTime CreatedDate { get; set; } // Дата и время расчета

        public string ClientName { get; set; } // Имя клиента или название детали
        public string Description { get; set; } // Краткое описание (например: "10мм, 20м")

        public double TotalPrice { get; set; } // Итоговая сумма
    }
}