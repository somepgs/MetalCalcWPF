namespace MetalCalcWPF.Models
{
    /// <summary>
    /// Сотрудник или представитель внешнего клиента — участник процесса заказа.
    ///
    /// <para>Один человек может быть и заявителем (подаёт заявку из своего цеха),
    /// и приёмщиком (мастер/бригадир/инженер ПТО, кто фиксирует заказ
    /// в нашем цехе металлообработки). Поэтому роль закодирована двумя
    /// независимыми флагами <see cref="CanSubmit"/> / <see cref="CanAccept"/>,
    /// а не enum-полем.</para>
    ///
    /// <para><see cref="WorkshopId"/> опционален: для внутренних сотрудников указывает
    /// цех приписки, для приёмщиков нашего цеха обычно ссылается на «Цех металлообработки»,
    /// для внешних — может быть NULL или ссылаться на запись клиента в Workshop.</para>
    /// </summary>
    public class Person
    {
        public int Id { get; set; }

        /// <summary>ФИО полностью: «Иванов Иван Иванович» или «Петров И.И.».</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Должность (свободный текст): Мастер, Бригадир, Инженер ПТО, Начальник цеха, Слесарь.</summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>FK на <see cref="Workshop.Id"/>. NULL — если человек не привязан к цеху.</summary>
        public int? WorkshopId { get; set; }

        /// <summary>Может подавать заявки на изготовление от имени своего цеха/клиента.</summary>
        public bool CanSubmit { get; set; } = true;

        /// <summary>Может принимать заказы (мастера, бригадиры, ПТО).</summary>
        public bool CanAccept { get; set; }

        /// <summary>Активен ли в справочнике (показывать в выпадающих списках).</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Свободная заметка: телефон, особенности взаимодействия.</summary>
        public string Notes { get; set; } = string.Empty;
    }
}
