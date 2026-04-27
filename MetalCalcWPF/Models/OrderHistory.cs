using System;

namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Запись в истории заказов цеха металлообработки.
    ///
    /// <para>Эволюция схемы:</para>
    /// <list type="bullet">
    ///   <item>v1 — базовые 6 полей (Id, CreatedDate, ClientName, Description, TotalPrice, OperationType)</item>
    ///   <item>v4 — добавлена cost-разбивка (MaterialCost / LaserCost / BendingCost / WeldingCost)</item>
    ///   <item>v6 — добавлены поля заявки (Priority, Quantity, MassKg, заявитель, цех, приёмщик, материал)</item>
    /// </list>
    ///
    /// <para>Поля заявки хранятся как <b>снапшоты строк</b>, а не FK на справочники.
    /// Причина: история заказов — это «фотография» договорённостей на момент создания
    /// заявки. Если пользователь удалит человека из <see cref="Person"/> или переименует
    /// цех в <see cref="Workshop"/>, исторический заказ должен остаться неизменным —
    /// иначе ломается аудит и отчётность за прошлые периоды.</para>
    /// </summary>
    public class OrderHistory
    {
        public int Id { get; set; }

        /// <summary>Дата поступления заявки (в Этапе 3 совпадает с моментом расчёта).</summary>
        public DateTime CreatedDate { get; set; }

        public string ClientName { get; set; } // Имя клиента или название детали — поле сохранено для совместимости
        public string Description { get; set; }

        public decimal TotalPrice { get; set; }

        public string OperationType { get; set; }

        // ====== Cost-разбивка (миграция v4) ======
        public decimal MaterialCost { get; set; }
        public decimal LaserCost { get; set; }
        public decimal BendingCost { get; set; }
        public decimal WeldingCost { get; set; }

        // ====== Поля заявки (миграция v6, Этап 3) ======

        /// <summary>Срочность. Default = Normal — для исторических заказов до v6.</summary>
        public OrderPriority Priority { get; set; } = OrderPriority.Normal;

        /// <summary>Количество деталей в заказе. Раньше было только в форме, теперь сохраняется.</summary>
        public int Quantity { get; set; }

        /// <summary>Расчётная масса всей партии (кг). Берётся из <c>CalculationResult</c>, удобно для отчёта.</summary>
        public double MassKg { get; set; }

        /// <summary>ФИО заявителя (снапшот из справочника <see cref="Person"/>).</summary>
        public string? ApplicantName { get; set; }

        /// <summary>Название цеха/клиента, от которого пришла заявка (снапшот из <see cref="Workshop"/>).</summary>
        public string? ApplicantWorkshopName { get; set; }

        /// <summary>ФИО принявшего заказ (мастер / бригадир / ПТО).</summary>
        public string? AcceptorName { get; set; }

        /// <summary>Марка материала (снапшот из <see cref="MaterialType.Name"/>).</summary>
        public string? MaterialName { get; set; }

        // ====== Workflow выполнения (миграция v8, Этап 4) ======

        /// <summary>
        /// Дата фактического выполнения заказа. NULL = заказ ещё в работе или в очереди.
        /// Заполняется одной кнопкой «Отметить как выполнен» из контекстного меню истории.
        /// <para>Заказы с NULL попадают в лист «Очередь» Excel-отчёта, отсортированные
        /// по срочности (Priority desc, CreatedDate asc).</para>
        /// </summary>
        public DateTime? CompletedDate { get; set; }
    }
}
