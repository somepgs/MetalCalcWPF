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

            if (bendProfile != null)
            {
                decimal pricePerBend;
                if (request.BendLengthMm <= 1500)
                    pricePerBend = (decimal)bendProfile.PriceLen1500;
                else if (request.BendLengthMm <= 3000)
                    pricePerBend = (decimal)bendProfile.PriceLen3000;
                else
                    pricePerBend = (decimal)bendProfile.PriceLen6000;

                decimal workCost = request.BendsCount * pricePerBend * request.Quantity;
                decimal setupCost = (decimal)bendProfile.SetupPrice;

                bendPriceTotal = workCost + setupCost;
                logFragment = $"+ Bend({request.BendsCount}x) ";
            }
            else
            {
                bendPriceTotal = request.BendsCount * settings.BendingBasePrice * request.Quantity;
                logFragment = string.Empty; // исходный код не добавлял фрагмент в этой ветке
            }

            result.BendingCost = bendPriceTotal;
            return logFragment;
        }
    }
}
