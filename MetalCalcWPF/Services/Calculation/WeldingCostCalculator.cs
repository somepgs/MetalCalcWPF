using System;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Считает стоимость сварки.
    ///
    /// Если в БД найден профиль сварки по толщине — полноценный профессиональный расчёт:
    /// время, проволока, газ, зарплата, расходники, наценка профиля.
    /// Если профиля нет — упрощённый fallback: длина × фиксированная цена за см × кол-во.
    ///
    /// Формулы выведены из исходного CalculationService v1 — НЕ менять без согласования.
    /// </summary>
    public class WeldingCostCalculator : IOperationCalculator
    {
        private readonly IDatabaseService _db;

        public WeldingCostCalculator(IDatabaseService db)
        {
            _db = db;
        }

        public string Name => "Welding";

        public string Apply(CalculationRequest request, WorkshopSettings settings, CalculationResult result)
        {
            if (!request.UseWelding || request.WeldLengthCm <= 0) return string.Empty;

            var weldProfile = _db.GetWeldingProfile(request.ThicknessMm);

            if (weldProfile != null)
            {
                // 1) Время сварки (мин)
                double weldTimeMinutes = weldProfile.WeldingSpeed > 0
                    ? request.WeldLengthCm / weldProfile.WeldingSpeed
                    : 0.0;

                // 2) Стоимость проволоки
                double weightPerCm = weldProfile.WeightPerCm > 0
                    ? weldProfile.WeightPerCm
                    : settings.WeldingWireConsumptionGPerCm;
                double totalWireGrams = weightPerCm * request.WeldLengthCm;
                decimal totalWireKg = (decimal)(totalWireGrams / 1000.0);
                decimal wireCost = totalWireKg * settings.WeldingWirePricePerKg;

                // 3) Стоимость газа
                decimal gasCost = settings.GetWeldingGasCostPerMinute() * (decimal)weldTimeMinutes;

                // 4) Зарплата и расходники (пропорционально времени)
                double totalMinutes = settings.WorkDaysPerMonth * settings.WorkHoursPerDay * 60;
                if (totalMinutes <= 0) totalMinutes = 1;
                decimal salaryPerMinute = settings.WelderMonthlySalary / (decimal)totalMinutes;
                decimal laborCost = salaryPerMinute * (decimal)weldTimeMinutes;
                decimal consumablesPerMinute = settings.WeldingConsumablesBudget / (decimal)totalMinutes;
                decimal consumablesCost = consumablesPerMinute * (decimal)weldTimeMinutes;

                // 5) Себестоимость + наценка
                decimal baseCost = wireCost + gasCost + laborCost + consumablesCost;
                double markup = weldProfile.MarkupCoefficient > 0
                    ? weldProfile.MarkupCoefficient
                    : settings.WeldingMarkupCoefficient;
                decimal priceWithMarkup = baseCost * (decimal)markup;

                decimal pricePerCm = request.WeldLengthCm > 0
                    ? priceWithMarkup / (decimal)request.WeldLengthCm
                    : 0m;

                decimal weldTotal = pricePerCm * (decimal)request.WeldLengthCm * request.Quantity;
                result.WeldingCost = weldTotal;

                result.WeldingDetails =
                    $"fillet={weldProfile.FilletSize}mm; speed={weldProfile.WeldingSpeed}см/мин; len={request.WeldLengthCm}см | " +
                    $"time={Math.Round(weldTimeMinutes, 2)}мин; wire={Math.Round(totalWireGrams, 2)}г ({Math.Round((double)wireCost, 2)}тг) | " +
                    $"gas={Math.Round((double)gasCost, 2)}тг; labor={Math.Round((double)laborCost, 2)}тг; consumables={Math.Round((double)consumablesCost, 2)}тг | " +
                    $"base={Math.Round((double)baseCost, 2)}тг; markup={markup}x; total={Math.Round((double)weldTotal):N0} тг";

                var breakdown = new CalculationBreakdown
                {
                    Section = "🏗️ Сварка",
                    Subtitle = $"катет {weldProfile.FilletSize} мм / скорость {weldProfile.WeldingSpeed} см/мин / длина {request.WeldLengthCm} см × {request.Quantity} шт",
                };
                breakdown.Lines.Add(new BreakdownLine(
                    "Время сварки",
                    $"{request.WeldLengthCm} см / {weldProfile.WeldingSpeed} см/мин",
                    (decimal)Math.Round(weldTimeMinutes, 2), "мин"));
                breakdown.Lines.Add(new BreakdownLine(
                    "Проволока",
                    $"{Math.Round(totalWireGrams, 2)} г × {settings.WeldingWirePricePerKg} тг/кг / 1000",
                    Math.Round(wireCost, 2)));
                breakdown.Lines.Add(new BreakdownLine(
                    "Газ",
                    $"{Math.Round(weldTimeMinutes, 2)} мин × цена газа/мин",
                    Math.Round(gasCost, 2)));
                breakdown.Lines.Add(new BreakdownLine(
                    "Зарплата сварщика",
                    $"{Math.Round(weldTimeMinutes, 2)} мин × ставка/мин",
                    Math.Round(laborCost, 2)));
                breakdown.Lines.Add(new BreakdownLine(
                    "Расходники",
                    $"{Math.Round(weldTimeMinutes, 2)} мин × бюджет/мин",
                    Math.Round(consumablesCost, 2)));
                breakdown.Lines.Add(new BreakdownLine(
                    "Себестоимость (без наценки)",
                    "проволока + газ + зп + расходники",
                    Math.Round(baseCost, 2)));
                breakdown.Lines.Add(new BreakdownLine(
                    "С наценкой",
                    $"{Math.Round(baseCost, 2)} × {markup}",
                    Math.Round(priceWithMarkup, 2)));
                breakdown.Lines.Add(new BreakdownLine(
                    $"Итого за сварку ({request.Quantity} шт)",
                    request.Quantity > 1
                        ? $"{Math.Round(priceWithMarkup, 2)} × {request.Quantity}"
                        : $"{Math.Round(priceWithMarkup, 2)}",
                    Math.Round(weldTotal, 2), "тг", IsTotal: true));
                result.Breakdowns.Add(breakdown);

                return $"+ Weld({request.WeldLengthCm}cm, {weldProfile.FilletSize}mm) ";
            }
            else
            {
                // Fallback — фиксированная цена за сантиметр
                decimal weldTotal = (decimal)request.WeldLengthCm * settings.WeldingCostPerCm * request.Quantity;
                result.WeldingCost = weldTotal;

                result.WeldingDetails =
                    $"Упрощенный расчет: {request.WeldLengthCm}см × {settings.WeldingCostPerCm} тг/см × {request.Quantity}шт = {Math.Round(weldTotal):N0} тг";

                var breakdown = new CalculationBreakdown
                {
                    Section = "🏗️ Сварка",
                    Subtitle = $"упрощённый расчёт (нет профиля по толщине {request.ThicknessMm} мм)",
                };
                breakdown.Lines.Add(new BreakdownLine(
                    "Цена за см",
                    "Настройки цеха",
                    settings.WeldingCostPerCm, "тг/см"));
                breakdown.Lines.Add(new BreakdownLine(
                    "Итого за сварку",
                    $"{request.WeldLengthCm} см × {settings.WeldingCostPerCm} × {request.Quantity} шт",
                    Math.Round(weldTotal, 2), "тг", IsTotal: true));
                result.Breakdowns.Add(breakdown);

                return $"+ Weld({request.WeldLengthCm}cm, basic) ";
            }
        }
    }
}
