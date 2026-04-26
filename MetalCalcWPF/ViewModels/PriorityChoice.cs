using MetalCalcWPF.Models;

namespace MetalCalcWPF.ViewModels
{
    /// <summary>
    /// Пара «значение enum → русский ярлык» для ComboBox срочности заказа.
    ///
    /// <para>WPF по умолчанию рисует enum'ы их CLR-именами (Low/Normal/Urgent).
    /// Чтобы пользователь видел «Низкая / Обычная / Высокая / Срочно» — биндим
    /// ComboBox с <c>SelectedValuePath="Value"</c> и <c>DisplayMemberPath="Label"</c>.
    /// SelectedValue остаётся именно <see cref="OrderPriority"/>, ничего парсить не надо.</para>
    /// </summary>
    public sealed class PriorityChoice
    {
        public PriorityChoice(OrderPriority value, string label)
        {
            Value = value;
            Label = label;
        }

        public OrderPriority Value { get; }
        public string Label { get; }
    }
}
