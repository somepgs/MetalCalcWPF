using System.Globalization;

namespace MetalCalcWPF.Utilities
{
    public static class NumberParser
    {
        public static bool TryParseDouble(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = text.Replace(",", ".").Trim();
            return double.TryParse(
                normalized,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
