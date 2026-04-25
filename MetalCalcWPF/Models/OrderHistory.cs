using System;

namespace MetalCalcWPF.Models
{
    public class OrderHistory
    {
        public int Id { get; set; } // Уникальный номер заказа (1, 2, 3...)

        public DateTime CreatedDate { get; set; } // Дата и время расчета

        public string ClientName { get; set; } // Имя клиента или название детали
        public string Description { get; set; } // Краткое описание (например: "10мм, 20м")

        public decimal TotalPrice { get; set; } // Итоговая сумма

        // Сводный «тип операции» — короткое имя для UI/группировок ("Металл + Лазер + Сварка").
        // На данных, сохранённых до v4, может содержать конкатенированный лог калькулятора.
        public string OperationType { get; set; }

        // ====== Cost-разбивка по операциям (миграция v4, Спринт 2.3+) ======
        // Записывается из CalculationResult.MaterialCost / LaserCost / BendingCost / WeldingCost.
        // На исторических заказах, сохранённых до v4, эти поля = 0 — это нормально,
        // отчёт корректно их обработает (в Excel такие строки попадут только в TotalPrice).

        /// <summary>Стоимость металла (тг). 0 для исторических заказов до v4.</summary>
        public decimal MaterialCost { get; set; }

        /// <summary>Стоимость лазерной резки (тг). 0 для исторических заказов до v4.</summary>
        public decimal LaserCost { get; set; }

        /// <summary>Стоимость гибки (тг). 0 для исторических заказов до v4.</summary>
        public decimal BendingCost { get; set; }

        /// <summary>Стоимость сварки (тг). 0 для исторических заказов до v4.</summary>
        public decimal WeldingCost { get; set; }
    }
}
