using System;
using System.Globalization;
using System.Windows.Data;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure
{
    /// <summary>
    /// Русские названия типов сортамента для UI.
    /// Используется в ComboBox / заголовках групп / фильтрах.
    /// </summary>
    public static class ProfileKindDisplay
    {
        public static string ToRu(ProfileKind kind) => kind switch
        {
            ProfileKind.Sheet         => "Лист г/к (ГОСТ 19903-2015)",
            ProfileKind.AngleEqual    => "Уголок равнополочный (ГОСТ 8509-93)",
            ProfileKind.AngleUnequal  => "Уголок неравнополочный (ГОСТ 8510-86)",
            ProfileKind.SquareTube    => "Профтруба квадратная (ГОСТ 30245-2003)",
            ProfileKind.RectTube      => "Профтруба прямоугольная (ГОСТ 30245-2003)",
            ProfileKind.Channel       => "Швеллер (ГОСТ 8240-97)",
            ProfileKind.IBeam         => "Двутавр (ГОСТ 8239-89)",
            _ => kind.ToString(),
        };
    }

    /// <summary>
    /// Конвертер: ProfileKind → русское название; null → "(все типы)".
    /// Работает и для nullable ProfileKind? в фильтрах.
    /// </summary>
    public class ProfileKindDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null) return "(все типы)";
            if (value is ProfileKind pk) return ProfileKindDisplay.ToRu(pk);
            return value.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
