using System;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Считает стоимость лазерной резки.
    ///
    /// Алгоритм (воспроизводит логику CalculationService v1 без изменений):
    /// 1. Профиль резки ищется по толщине: <see cref="IDatabaseService.GetProfileByThickness"/>.
    /// 2. Время реза = длина/скорость (мин), переводится в часы.
    /// 3. Ставка станка — кислородная или воздушная (<see cref="WorkshopSettings.GetHourlyBaseCost"/>).
    /// 4. Для кислородного реза прибавляется стоимость газа за всё время.
    /// 5. Врезки (pierces): время × ставка, плюс фиксированная цена за каждую.
    /// 6. На полную цену реза накладывается процент наценки профиля.
    /// 7. Плюс setup-затраты на один прогон и минимальная цена работы лазера.
    /// 8. Для толстого металла добавляется плата за обработку.
    ///
    /// НЕ менять формулы без согласования — влияют на КП клиенту напрямую.
    /// </summary>
    public class LaserCostCalculator : IOperationCalculator
    {
        private readonly IDatabaseService _db;

        public LaserCostCalculator(IDatabaseService db)
        {
            _db = db;
        }

        public string Name => "Laser";

        public string Apply(CalculationRequest request, WorkshopSettings settings, CalculationResult result)
        {
            if (request.LaserLengthMeters <= 0) return string.Empty;

            var profile = _db.GetProfileByThickness(request.ThicknessMm);
            if (profile == null) return string.Empty;

            double cuttingTimeMinutes;
            double cuttingTimeHours;
            if (profile.CuttingSpeed <= 0)
            {
                cuttingTimeMinutes = 0;
                cuttingTimeHours = 0;
            }
            else
            {
                cuttingTimeMinutes = request.LaserLengthMeters / profile.CuttingSpeed;
                cuttingTimeHours = cuttingTimeMinutes / 60.0;
            }

            bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
            decimal machineCostPerHour = settings.GetHourlyBaseCost(isAir);

            decimal oxygenCost = 0m;
            if (!isAir)
            {
                decimal oxygenCostPerMinute = settings.GetOxygenCostPerMinute();
                oxygenCost = oxygenCostPerMinute * (decimal)cuttingTimeMinutes * request.Quantity;
            }

            decimal costPrice = (decimal)cuttingTimeHours * machineCostPerHour;

            double pierceTimeMinutes = (request.PiercesCount * settings.PierceTimeSeconds) / 60.0;
            double pierceTimeHours = pierceTimeMinutes / 60.0;
            decimal pierceCost = (decimal)pierceTimeHours * machineCostPerHour;

            decimal costPriceWithPierces = costPrice + pierceCost;

            decimal priceForCutting = costPriceWithPierces * (1 + (decimal)profile.MarkupCoefficient / 100m);
            priceForCutting += settings.LaserSetupCostPerJob / Math.Max(1, request.Quantity);
            if (priceForCutting * request.Quantity < settings.LaserMinChargePerJob)
            {
                priceForCutting = settings.LaserMinChargePerJob / (decimal)Math.Max(1, request.Quantity);
            }

            decimal priceForPierces = (decimal)profile.PiercePrice * request.PiercesCount;

            // Для детализации нужна себестоимость именно с врезками:
            costPrice = costPriceWithPierces;

            decimal handlingExtra = 0m;
            if (request.ThicknessMm > settings.HeavyMaterialThresholdMm)
                handlingExtra = settings.HeavyHandlingCostPerDetail;

            decimal laserTotalPerOne = priceForCutting + priceForPierces + handlingExtra
                                       + (oxygenCost / Math.Max(1, request.Quantity));

            result.LaserCost = laserTotalPerOne * request.Quantity;

            result.LaserDetails =
                $"cutLen={request.LaserLengthMeters}m; speed={profile.CuttingSpeed}m/min; time={Math.Round(cuttingTimeMinutes, 2)}min ({Math.Round(cuttingTimeHours, 4)}h) | " +
                $"machineRate={Math.Round(machineCostPerHour):N0} тг/ч; baseCost={Math.Round(costPrice, 2)} тг | " +
                $"cutPrice(one)={Math.Round((double)priceForCutting, 2)} тг; pierce(one)={Math.Round((double)priceForPierces, 2)} тг ({request.PiercesCount}шт) | " +
                $"oxygenTotal={Math.Round((double)oxygenCost):N0} тг; handling(one)={Math.Round((double)handlingExtra, 2)} тг";

            return $"+ Laser({request.PiercesCount}x pierce) ";
        }
    }
}
