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

                return $"+ Weld({request.WeldLengthCm}cm, {weldProfile.FilletSize}mm) ";
            }
            else
            {
                // Fallback — фиксированная цена за сантиметр
                decimal weldTotal = (decimal)request.WeldLengthCm * settings.WeldingCostPerCm * request.Quantity;
                result.WeldingCost = weldTotal;

                result.WeldingDetails =
                    $"Упрощенный расчет: {request.WeldLengthCm}см × {settings.WeldingCostPerCm} тг/см × {request.Quantity}шт = {Math.Round(weldTotal):N0} тг";

                return $"+ Weld({request.WeldLengthCm}cm, basic) ";
            }
        }
    }
}
