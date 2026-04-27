using System;
using System.Globalization;
using System.Windows.Data;

namespace MetalCalcWPF.Infrastructure
{
    /// <summary>
    /// Конвертер «null → строка А; не-null → строка Б».
    ///
    /// <para>Используется в контекстном меню истории заказов: один пункт меню
    /// меняет заголовок в зависимости от <c>OrderHistory.CompletedDate</c>:</para>
    /// <code>
    /// &lt;MenuItem Header="{Binding ..CompletedDate,
    ///                              Converter={StaticResource NullToToggleConverter},
    ///                              ConverterParameter='✅ Отметить как выполнен|↩ Снять отметку выполнения'}" /&gt;
    /// </code>
    ///
    /// <para>Параметр — две строки через <c>'|'</c>: первая для null, вторая для не-null.
    /// Если параметр не задан — возвращает значение как есть (или пустую строку).</para>
    /// </summary>
    public class NullToToggleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not string raw) return value?.ToString() ?? string.Empty;

            var parts = raw.Split('|');
            if (parts.Length < 2) return raw;

            return value is null ? parts[0] : parts[1];
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
