using System;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services
{
    // Класс для результата (Чек)
    public class CalculationResult
    {
        public double MaterialCost { get; set; }  // Цена металла
        public double LaserCost { get; set; }     // Цена резки
        public double BendingCost { get; set; }   // Цена гибки
        public double WeldingCost { get; set; }   // Цена сварки

        public double TotalPrice => MaterialCost + LaserCost + BendingCost + WeldingCost;

        public string Log { get; set; }           // История (что посчитали)

        // ✅ НОВОЕ: Детализация расчета лазера
        public string LaserDetails { get; set; }  // Подробности расчета лазера
    }

    public class CalculationService
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
            int piercesCount, // ✅ НОВОЕ: Количество отверстий (пробивок)
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
                // Вес ОДНОЙ детали в кг
                double weightKgPerPart;
                bool hasMeasuredWeight = measuredWeightKg > 0;

                if (hasMeasuredWeight)
                {
                    weightKgPerPart = measuredWeightKg / quantity;
                }
                else
                {
                    weightKgPerPart = (widthMm * heightMm * thicknessMm * material.Density) / 1_000_000.0;
                }

                // Вес всей партии
                double totalWeightKg = weightKgPerPart * quantity;

                // Цена закупа (Сетка цен для Ст3)
                double costPricePerKg = material.BasePricePerKg;
                if (material.Name.Contains("Ст3"))
                {
                    if (thicknessMm <= 12) costPricePerKg = 355;
                    else if (thicknessMm <= 25) costPricePerKg = 375;
                    else costPricePerKg = 385;
                }

                // Цена продажи = Закуп * (1 + Наценка%)
                double sellPricePerKg = costPricePerKg * (1 + settings.MaterialMarkupPercent / 100.0);

                result.MaterialCost = totalWeightKg * sellPricePerKg;

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
                    // А. Время резки (часы)
                    double cuttingTimeHours = (laserLengthMeters / profile.CuttingSpeed) / 60.0;
                    double cuttingTimeMinutes = cuttingTimeHours * 60.0;

                    // Б. Определяем тип газа
                    bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";

                    // В. Стоимость часа работы станка (ЗП + Свет + Амортизация)
                    double machineCostPerHour = settings.GetHourlyBaseCost(isAir);

                    // ✅ НОВЫЙ РАСЧЕТ КИСЛОРОДА (ТОЧНЫЙ)
                    double oxygenCost = 0;
                    if (!isAir)
                    {
                        // Параметры из настроек
                        double oxygenBottleVolumeLiters = settings.OxygenBottleVolumeLiters; // 40 литров
                        double oxygenBottlePressureAtm = settings.OxygenBottlePressureAtm;   // 150 атм
                        double oxygenFlowRateLpm = settings.OxygenFlowRateLpm;               // 15 л/мин
                        double oxygenBottlePrice = settings.OxygenBottlePrice;               // 5000 тг

                        // Общий объем кислорода в баллоне при атмосферном давлении
                        double totalOxygenLiters = oxygenBottleVolumeLiters * oxygenBottlePressureAtm;

                        // Время работы одного баллона (минуты)
                        double bottleWorkTimeMinutes = totalOxygenLiters / oxygenFlowRateLpm;

                        // Цена кислорода за минуту
                        double oxygenCostPerMinute = oxygenBottlePrice / bottleWorkTimeMinutes;

                        // Стоимость кислорода для этой детали
                        oxygenCost = oxygenCostPerMinute * cuttingTimeMinutes * quantity;
                    }

                    // Г. Себестоимость резки = Время * Тариф
                    double costPrice = cuttingTimeHours * machineCostPerHour;

                    // Д. Цена для клиента = Себестоимость * Наценку (Markup)
                    double priceForCutting = costPrice * profile.MarkupCoefficient;

                    // ✅ НОВЫЙ РАСЧЕТ ПРОБИВОК (множитель на количество отверстий)
                    double priceForPierces = profile.PiercePrice * piercesCount;

                    // Е. Сложность (Кувалда)
                    double handlingExtra = 0;
                    if (thicknessMm > settings.HeavyMaterialThresholdMm)
                        handlingExtra = settings.HeavyHandlingCostPerDetail;

                    // Итого за 1 деталь
                    double laserTotalPerOne = priceForCutting + priceForPierces + handlingExtra + (oxygenCost / quantity);

                    result.LaserCost = laserTotalPerOne * quantity;

                    // ✅ НОВОЕ: Детализация для отладки
                    result.LaserDetails = $"Резка: {Math.Round(priceForCutting * quantity):N0} ₸ | " +
                                         $"Пробивки ({piercesCount}шт): {Math.Round(priceForPierces * quantity):N0} ₸ | " +
                                         $"Кислород: {Math.Round(oxygenCost):N0} ₸ | " +
                                         $"Тяжесть: {Math.Round(handlingExtra * quantity):N0} ₸";

                    logBuilder += $"+ Laser({piercesCount}x pierce) ";
                }
            }

            // --- 3. ГИБКА (Bending) ---
            if (useBending && bendsCount > 0)
            {
                var bendProfile = _db.GetBendingProfile(thicknessMm);
                double bendPriceTotal = 0;

                if (bendProfile != null)
                {
                    // Цена за 1 гиб (по длине)
                    double pricePerBend = 0;
                    if (bendLengthMm <= 1500)
                        pricePerBend = bendProfile.PriceLen1500;
                    else if (bendLengthMm <= 3000)
                        pricePerBend = bendProfile.PriceLen3000;
                    else
                        pricePerBend = bendProfile.PriceLen6000;

                    // Работа: Гибы * Цена * Кол-во деталей
                    double workCost = bendsCount * pricePerBend * quantity;

                    // Наладка: Одна на всю партию
                    double setupCost = bendProfile.SetupPrice;

                    bendPriceTotal = workCost + setupCost;
                    logBuilder += $"+ Bend({bendsCount}x) ";
                }
                else
                {
                    // Резерв (если профиль не найден)
                    bendPriceTotal = bendsCount * settings.BendingBasePrice * quantity;
                }

                result.BendingCost = bendPriceTotal;
            }

            // --- 4. СВАРКА (Welding) ---
            if (useWelding && weldLengthCm > 0)
            {
                double weldTotal = weldLengthCm * settings.WeldingCostPerCm * quantity;
                result.WeldingCost = weldTotal;
                logBuilder += $"+ Weld({weldLengthCm}cm) ";
            }

            result.Log = logBuilder.Trim();
            return result;
        }
    }
}