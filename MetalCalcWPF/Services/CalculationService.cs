using System;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services
{
    public class CalculationResult
    {
        public decimal MaterialCost { get; set; }
        public decimal LaserCost { get; set; }
        public decimal BendingCost { get; set; }
        public decimal WeldingCost { get; set; }

        public decimal TotalPrice => MaterialCost + LaserCost + BendingCost + WeldingCost;

        public string Log { get; set; }
        public string LaserDetails { get; set; }
        
        // ✅ НОВОЕ: Детализация расчета сварки
        public string WeldingDetails { get; set; }
    }

    public class CalculationService : ICalculationService
    {
        private readonly IDatabaseService _db;

        public CalculationService(IDatabaseService db)
        {
            _db = db;
        }

        /// <summary>
        /// Главный метод расчета стоимости заказа
        /// </summary>
        public CalculationResult CalculateOrder(
            double widthMm, double heightMm, double thicknessMm,
            int quantity,
            MaterialType? material,
            double laserLengthMeters,
            int piercesCount,
            bool useBending, int bendsCount, double bendLengthMm,
            bool useWelding, double weldLengthCm,
            double measuredWeightKg = 0)
        {
            var result = new CalculationResult();
            var settings = _db.GetSettings();
            string logBuilder = "";

            // --- 1. МЕТАЛЛ (Material) ---
            if (((widthMm > 0 && heightMm > 0) || measuredWeightKg > 0) && material != null)
            {
                double weightKgPerPart;
                bool hasMeasuredWeight = measuredWeightKg > 0 && quantity > 0;

                if (hasMeasuredWeight && quantity > 0)
                {
                    weightKgPerPart = measuredWeightKg / quantity;
                }
                else
                {
                    double w = widthMm > 0 ? widthMm : 0;
                    double h = heightMm > 0 ? heightMm : 0;
                    double t = thicknessMm > 0 ? thicknessMm : 0;
                    weightKgPerPart = (w * h * t * material.Density) / 1_000_000.0;
                }

                double totalWeightKg = weightKgPerPart * quantity;
                decimal costPricePerKg = material.BasePricePerKg;
                decimal sellPricePerKg = costPricePerKg * (1 + settings.MaterialMarkupPercent / 100m);

                result.MaterialCost = (decimal)totalWeightKg * sellPricePerKg;

                logBuilder += hasMeasuredWeight
                    ? $"Metal({Math.Round(totalWeightKg, 1)}kg total) "
                    : $"Metal({Math.Round(weightKgPerPart, 1)}kg x {quantity}) ";
            }

            // --- 2. ЛАЗЕР (Laser) ---
            if (laserLengthMeters > 0)
            {
                var profile = _db.GetProfileByThickness(thicknessMm);
                if (profile != null)
                {
                    double cuttingTimeMinutes = 0;
                    double cuttingTimeHours = 0;
                    if (profile.CuttingSpeed <= 0)
                    {
                        cuttingTimeMinutes = 0;
                        cuttingTimeHours = 0;
                    }
                    else
                    {
                        cuttingTimeMinutes = (laserLengthMeters / profile.CuttingSpeed);
                        cuttingTimeHours = cuttingTimeMinutes / 60.0;
                    }

                    bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
                    decimal machineCostPerHour = settings.GetHourlyBaseCost(isAir);

                    decimal oxygenCost = 0m;
                    if (!isAir)
                    {
                        decimal oxygenCostPerMinute = settings.GetOxygenCostPerMinute();
                        oxygenCost = oxygenCostPerMinute * (decimal)cuttingTimeMinutes * quantity;
                    }

                    decimal costPrice = (decimal)cuttingTimeHours * machineCostPerHour;

                    double pierceTimeMinutes = (piercesCount * settings.PierceTimeSeconds) / 60.0;
                    decimal pierceCost = (decimal)pierceTimeMinutes * machineCostPerHour;

                    decimal costPriceWithPierces = costPrice + pierceCost;

                    decimal priceForCutting = costPriceWithPierces * (1 + (decimal)profile.MarkupCoefficient / 100m);
                    priceForCutting += settings.LaserSetupCostPerJob / Math.Max(1, quantity);
                    if (priceForCutting * quantity < settings.LaserMinChargePerJob)
                    {
                        priceForCutting = settings.LaserMinChargePerJob / (decimal)Math.Max(1, quantity);
                    }

                    decimal priceForPierces = (decimal)profile.PiercePrice * piercesCount;

                    costPrice = costPriceWithPierces;

                    decimal handlingExtra = 0m;
                    if (thicknessMm > settings.HeavyMaterialThresholdMm)
                        handlingExtra = settings.HeavyHandlingCostPerDetail;

                    decimal laserTotalPerOne = priceForCutting + priceForPierces + handlingExtra + (oxygenCost / Math.Max(1, quantity));

                    result.LaserCost = laserTotalPerOne * quantity;

                    result.LaserDetails =
                        $"cutLen={laserLengthMeters}m; speed={profile.CuttingSpeed}m/min; time={Math.Round(cuttingTimeMinutes,2)}min ({Math.Round(cuttingTimeHours,4)}h) | " +
                        $"machineRate={Math.Round(machineCostPerHour):N0} тг/ч; baseCost={Math.Round(costPrice,2)} тг | " +
                        $"cutPrice(one)={Math.Round((double)priceForCutting,2)} тг; pierce(one)={Math.Round((double)priceForPierces,2)} тг ({piercesCount}шт) | " +
                        $"oxygenTotal={Math.Round((double)oxygenCost):N0} тг; handling(one)={Math.Round((double)handlingExtra,2)} тг";

                    logBuilder += $"+ Laser({piercesCount}x pierce) ";
                }
            }

            // --- 3. ГИБКА (Bending) ---
            if (useBending && bendsCount > 0)
            {
                var bendProfile = _db.GetBendingProfile(thicknessMm);
                decimal bendPriceTotal = 0m;

                if (bendProfile != null)
                {
                    decimal pricePerBend = 0m;
                    if (bendLengthMm <= 1500)
                        pricePerBend = (decimal)bendProfile.PriceLen1500;
                    else if (bendLengthMm <= 3000)
                        pricePerBend = (decimal)bendProfile.PriceLen3000;
                    else
                        pricePerBend = (decimal)bendProfile.PriceLen6000;

                    decimal workCost = bendsCount * pricePerBend * quantity;
                    decimal setupCost = (decimal)bendProfile.SetupPrice;

                    bendPriceTotal = workCost + setupCost;
                    logBuilder += $"+ Bend({bendsCount}x) ";
                }
                else
                {
                    bendPriceTotal = bendsCount * settings.BendingBasePrice * quantity;
                }

                result.BendingCost = bendPriceTotal;
            }

            // --- 4. СВАРКА (Welding) - ✅ НОВЫЙ ПРОФЕССИОНАЛЬНЫЙ РАСЧЕТ ---
            if (useWelding && weldLengthCm > 0)
            {
                // Ищем профиль сварки по толщине металла
                var weldProfile = _db.GetWeldingProfile(thicknessMm);

                if (weldProfile != null)
                {
                    // Новый профессиональный расчет на основе профиля и общих настроек
                    // 1) Время сварки (мин)
                    double weldTimeMinutes = weldProfile.WeldingSpeed > 0 ? weldLengthCm / weldProfile.WeldingSpeed : 0.0;

                    // 2) Стоимость проволоки
                    double weightPerCm = weldProfile.WeightPerCm > 0 ? weldProfile.WeightPerCm : settings.WeldingWireConsumptionGPerCm;
                    double totalWireGrams = weightPerCm * weldLengthCm;
                    decimal totalWireKg = (decimal)(totalWireGrams / 1000.0);
                    decimal wireCost = totalWireKg * settings.WeldingWirePricePerKg;

                    // 3) Стоимость газа
                    decimal gasCost = settings.GetWeldingGasCostPerMinute() * (decimal)weldTimeMinutes;

                    // 4) Заработная плата и расходники (за время)
                    double totalMinutes = settings.WorkDaysPerMonth * settings.WorkHoursPerDay * 60;
                    if (totalMinutes <= 0) totalMinutes = 1;
                    decimal salaryPerMinute = settings.WelderMonthlySalary / (decimal)totalMinutes;
                    decimal laborCost = salaryPerMinute * (decimal)weldTimeMinutes;
                    decimal consumablesPerMinute = settings.WeldingConsumablesBudget / (decimal)totalMinutes;
                    decimal consumablesCost = consumablesPerMinute * (decimal)weldTimeMinutes;

                    // 5) Себестоимость и наценка
                    decimal baseCost = wireCost + gasCost + laborCost + consumablesCost;

                    double markup = weldProfile.MarkupCoefficient > 0 ? weldProfile.MarkupCoefficient : settings.WeldingMarkupCoefficient;
                    decimal priceWithMarkup = baseCost * (decimal)markup;

                    // Цена за 1 см (для детализации)
                    decimal pricePerCm = weldLengthCm > 0 ? priceWithMarkup / (decimal)weldLengthCm : 0m;

                    decimal weldTotal = pricePerCm * (decimal)weldLengthCm * quantity;
                    result.WeldingCost = weldTotal;

                    // Детализация
                    result.WeldingDetails =
                        $"fillet={weldProfile.FilletSize}mm; speed={weldProfile.WeldingSpeed}см/мин; len={weldLengthCm}см | " +
                        $"time={Math.Round(weldTimeMinutes, 2)}мин; wire={Math.Round(totalWireGrams,2)}г ({Math.Round((double)wireCost,2)}тг) | " +
                        $"gas={Math.Round((double)gasCost,2)}тг; labor={Math.Round((double)laborCost,2)}тг; consumables={Math.Round((double)consumablesCost,2)}тг | " +
                        $"base={Math.Round((double)baseCost,2)}тг; markup={markup}x; total={Math.Round((double)weldTotal):N0} тг";

                    logBuilder += $"+ Weld({weldLengthCm}cm, {weldProfile.FilletSize}mm) ";
                }
                else
                {
                    // Резервный расчет, если профиль не найден: используем фиксированную стоимость за см
                    decimal weldTotal = (decimal)weldLengthCm * settings.WeldingCostPerCm * quantity;
                    result.WeldingCost = weldTotal;

                    result.WeldingDetails = $"Упрощенный расчет: {weldLengthCm}см × {settings.WeldingCostPerCm} тг/см × {quantity}шт = {Math.Round(weldTotal):N0} тг";
                    logBuilder += $"+ Weld({weldLengthCm}cm, basic) ";
                }
            }

            result.Log = logBuilder.Trim();
            return result;
        }
    }
}
