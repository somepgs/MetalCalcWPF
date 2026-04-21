using System;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Считает стоимость металла для заказа.
    ///
    /// Две стратегии расчёта массы:
    /// 1) <b>По измеренной массе</b> — если <see cref="CalculationRequest.MeasuredWeightKg"/> &gt; 0,
    ///    берём её как полную массу партии (используется режимом «Сортамент проката»:
    ///    длина × кг/м × кол-во уже посчитаны во вьюмодели).
    /// 2) <b>По объёму листа</b> — иначе Ш × В × Т × плотность / 1 000 000.
    ///
    /// Цена продажи = закупочная × (1 + наценка/100).
    /// Формулы выведены из исходного CalculationService v1 — при рефакторинге
    /// НЕ менять без обсуждения, это критично для точности сметы.
    /// </summary>
    public class MaterialCostCalculator : IOperationCalculator
    {
        public string Name => "Material";

        public string Apply(CalculationRequest request, WorkshopSettings settings, CalculationResult result)
        {
            if (request.Material == null) return string.Empty;

            bool hasDimensions = request.WidthMm > 0 && request.HeightMm > 0;
            bool hasMeasuredWeight = request.MeasuredWeightKg > 0 && request.Quantity > 0;

            if (!hasDimensions && !hasMeasuredWeight) return string.Empty;

            double weightKgPerPart;
            if (hasMeasuredWeight)
            {
                weightKgPerPart = request.MeasuredWeightKg / request.Quantity;
            }
            else
            {
                double w = request.WidthMm > 0 ? request.WidthMm : 0;
                double h = request.HeightMm > 0 ? request.HeightMm : 0;
                double t = request.ThicknessMm > 0 ? request.ThicknessMm : 0;
                weightKgPerPart = (w * h * t * request.Material.Density) / 1_000_000.0;
            }

            double totalWeightKg = weightKgPerPart * request.Quantity;
            decimal costPricePerKg = request.Material.BasePricePerKg;
            decimal sellPricePerKg = costPricePerKg * (1 + settings.MaterialMarkupPercent / 100m);

            result.MaterialCost = (decimal)totalWeightKg * sellPricePerKg;

            // Прозрачная детализация: откуда взялась масса, какая наценка на материал.
            var breakdown = new CalculationBreakdown
            {
                Section = "📦 Металл",
                Subtitle = hasMeasuredWeight
                    ? $"{request.Material.Name} / масса партии задана ({Math.Round(totalWeightKg, 2)} кг)"
                    : $"{request.Material.Name} / {request.WidthMm}×{request.HeightMm}×{request.ThicknessMm} мм / ρ={request.Material.Density}",
            };
            if (hasMeasuredWeight)
            {
                breakdown.Lines.Add(new BreakdownLine(
                    "Масса партии",
                    "указана оператором",
                    (decimal)totalWeightKg, "кг"));
            }
            else
            {
                breakdown.Lines.Add(new BreakdownLine(
                    "Масса 1 шт",
                    $"{request.WidthMm} × {request.HeightMm} × {request.ThicknessMm} × {request.Material.Density} / 1 000 000",
                    (decimal)Math.Round(weightKgPerPart, 4), "кг"));
                breakdown.Lines.Add(new BreakdownLine(
                    $"Масса {request.Quantity} шт",
                    $"{Math.Round(weightKgPerPart, 4)} × {request.Quantity}",
                    (decimal)Math.Round(totalWeightKg, 4), "кг"));
            }
            breakdown.Lines.Add(new BreakdownLine(
                "Закупочная цена",
                $"{request.Material.Name}",
                costPricePerKg, "тг/кг"));
            if (settings.MaterialMarkupPercent != 0)
            {
                breakdown.Lines.Add(new BreakdownLine(
                    "Цена продажи за кг",
                    $"{costPricePerKg} × (1 + {settings.MaterialMarkupPercent}% наценки)",
                    Math.Round(sellPricePerKg, 2), "тг/кг"));
            }
            breakdown.Lines.Add(new BreakdownLine(
                "Итого за металл",
                $"{Math.Round(totalWeightKg, 2)} кг × {Math.Round(sellPricePerKg, 2)} тг/кг",
                Math.Round(result.MaterialCost, 2), "тг", IsTotal: true));
            result.Breakdowns.Add(breakdown);

            return hasMeasuredWeight
                ? $"Metal({Math.Round(totalWeightKg, 1)}kg total) "
                : $"Metal({Math.Round(weightKgPerPart, 1)}kg x {request.Quantity}) ";
        }
    }
}
