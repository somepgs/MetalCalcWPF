using System.Globalization;
using System.Windows.Controls;
using MetalCalcWPF.Utilities;

namespace MetalCalcWPF.Infrastructure
{
    public class PositiveDoubleValidationRule : ValidationRule
    {
        public bool AllowZero { get; set; } = false;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = (value ?? string.Empty).ToString();
            if (!NumberParser.TryParseDouble(str, out var v))
                return new ValidationResult(false, "Не число");
            if (AllowZero)
            {
                if (v < 0) return new ValidationResult(false, "Должно быть >= 0");
            }
            else
            {
                if (v <= 0) return new ValidationResult(false, "Должно быть > 0");
            }
            return ValidationResult.ValidResult;
        }
    }

    public class NonNegativeDoubleValidationRule : PositiveDoubleValidationRule
    {
        public NonNegativeDoubleValidationRule() { AllowZero = true; }
    }

    public class IntegerValidationRule : ValidationRule
    {
        public bool AllowZero { get; set; } = false;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = (value ?? string.Empty).ToString();
            if (!NumberParser.TryParseDouble(str, out var d))
                return new ValidationResult(false, "Не число");
            if (d % 1 != 0) return new ValidationResult(false, "Должно быть целым числом");
            if (AllowZero)
            {
                if (d < 0) return new ValidationResult(false, "Должно быть >= 0");
            }
            else
            {
                if (d <= 0) return new ValidationResult(false, "Должно быть > 0");
            }
            return ValidationResult.ValidResult;
        }
    }
}
