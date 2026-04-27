using MetalCalcWPF.Models;

namespace MetalCalcWPF.ViewModels
{
    /// <summary>
    /// Пара «значение enum → русский ярлык» для ComboBox типа цеха/клиента.
    /// Аналог <see cref="PriorityChoice"/> — без него DataGridComboBox показывает
    /// CLR-имена вроде «Internal/ExternalClient», что для пользователя неприемлемо.
    /// </summary>
    public sealed class WorkshopKindChoice
    {
        public WorkshopKindChoice(WorkshopKind value, string label)
        {
            Value = value;
            Label = label;
        }

        public WorkshopKind Value { get; }
        public string Label { get; }
    }
}
