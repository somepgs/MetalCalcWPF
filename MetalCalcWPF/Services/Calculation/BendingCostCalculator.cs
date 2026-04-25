using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Считает стоимость гибки.
    ///
    /// 1) Если в БД есть профиль по толщине — цена одного гиба берётся по диапазону длины
    ///    (&lt;=1500 мм / &lt;=3000 / иначе 6000), плюс разовый setup-платёж.
    /// 2) Если профиля нет — fallback на <see cref="WorkshopSettings.BendingBasePrice"/>
    ///    (base × кол-во гибов × кол-во деталей).
    ///
    /// Формулы выведены из исходного CalculationService v1.
    /// </summary>
    public class BendingCostCalculator : IOperationCalculator
    {
        private readonly IDatabaseService _db;

        public BendingCostCalculator(IDatabaseService db)
        {
            _db = db;
        }

        public string Name => "Bending";

        public string Apply(CalculationRequest request, WorkshopSettings settings, CalculationResult result)
        {
            if (!request.UseBending || request.BendsCount <= 0) return string.Empty;

            var bendProfile = _db.GetBendingProfile(request.ThicknessMm);
            decimal bendPriceTotal;
            string logFragment;

            var breakdown = new CalculationBreakdown
            {
                Section = "📐 Гибка",
                Subtitle = $"{request.ThicknessMm} мм / {request.BendsCount} гиб(ов) × {request.Quantity} шт / длина линии {request.BendLengthMm} мм",
            };

            if (bendProfile != null)
            {
                decimal pricePerBend;
                string rangeLabel;
                if (request.BendLengthMm <= 1500)
                {
                    pricePerBend = (decimal)bendProfile.PriceLen1500;
                    rangeLabel = "до 1500 мм";
                }
                else if (request.BendLengthMm <= 3000)
                {
                    pricePerBend = (decimal)bendProfile.PriceLen3000;
                    rangeLabel = "до 3000 мм";
                }
                else
                {
                    pricePerBend = (decimal)bendProfile.PriceLen6000;
                    rangeLabel = "до 6000 мм";
                }

                decimal workCost = request.BendsCount * pricePerBend * request.Quantity;
                decimal setupCost = (decimal)bendProfile.SetupPrice;

                bendPriceTotal = workCost + setupCost;
                logFragment = $"+ Гибка({request.BendsCount}×) ";

                breakdown.Lines.Add(new BreakdownLine(
                    "Цена одного гиба",
                    $"диапазон {rangeLabel} (профиль гибки {request.ThicknessMm} мм)",
                    pricePerBend));
                breakdown.Lines.Add(new BreakdownLine(
                    "Работа по гибке",
                    $"{request.BendsCount} гиб(ов) × {pricePerBend} × {request.Quantity} шт",
                    Math.Round(workCost, 2)));
                if (setupCost > 0)
                {
                    breakdown.Lines.Add(new BreakdownLine(
                        "Разовая наладка",
                        "Setup профиля гибки",
                        setupCost));
                }
                breakdown.Lines.Add(new BreakdownLine(
                    "Итого за гибку",
                    setupCost > 0
                        ? $"{Math.Round(workCost, 2)} + {setupCost}"
                        : $"{Math.Round(workCost, 2)}",
                    Math.Round(bendPriceTotal, 2), "тг", IsTotal: true));
            }
            else
            {
                bendPriceTotal = request.BendsCount * settings.BendingBasePrice * request.Quantity;
                logFragment = string.Empty; // исходный код не добавлял фрагмент в этой ветке

                breakdown.Lines.Add(new BreakdownLine(
                    "Базовая цена гиба",
                    "Настройки (нет профиля по толщине)",
                    settings.BendingBasePrice));
                breakdown.Lines.Add(new BreakdownLine(
                    "Итого за гибку",
                    $"{request.BendsCount} × {settings.BendingBasePrice} × {request.Quantity}",
                    Math.Round(bendPriceTotal, 2), "тг", IsTotal: true));
            }

            result.BendingCost = bendPriceTotal;
            result.Breakdowns.Add(breakdown);
            return logFragment;
        }
    }
}
