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
                bool hasMeasuredWeight = measuredWeightKg > 0 && quantity > 0;

                if (hasMeasuredWeight && quantity > 0)
                {
                    weightKgPerPart = measuredWeightKg / quantity;
                }
                else
                {
                    // Защита от нулевых габаритов
                    double w = widthMm > 0 ? widthMm : 0;
                    double h = heightMm > 0 ? heightMm : 0;
                    double t = thicknessMm > 0 ? thicknessMm : 0;
                    weightKgPerPart = (w * h * t * material.Density) / 1_000_000.0;
                }

                // Вес всей партии
                double totalWeightKg = weightKgPerPart * quantity;

                // Цена закупа — используем значение из справочника (убираем хардкод)
                double costPricePerKg = material.BasePricePerKg;

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
                    double cuttingTimeMinutes = 0;
                    double cuttingTimeHours = 0;
                    if (profile.CuttingSpeed <= 0)
                    {
                        // Защита от некорректной скорости
                        cuttingTimeMinutes = 0;
                        cuttingTimeHours = 0;
                    }
                    else
                    {
                        cuttingTimeMinutes = (laserLengthMeters / profile.CuttingSpeed);
                        cuttingTimeHours = cuttingTimeMinutes / 60.0;
                    }

                    // Б. Определяем тип газа
                    bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";

                    // В. Стоимость часа работы станка (ЗП + Свет + Амортизация)
                    double machineCostPerHour = settings.GetHourlyBaseCost(isAir);

                    // ✅ НОВЫЙ РАСЧЕТ КИСЛОРОДА (ТОЧНЫЙ) — используем метод настроек
                    double oxygenCost = 0;
                    if (!isAir)
                    {
                        double oxygenCostPerMinute = settings.GetOxygenCostPerMinute();
                        oxygenCost = oxygenCostPerMinute * cuttingTimeMinutes * quantity;
                    }

                    // Г. Себестоимость резки = Время * Тариф
                    double costPrice = cuttingTimeHours * machineCostPerHour;

                    // Учитываем время пробивок в себестоимости: пробивки занимают время и потребляют ресурс
                    double pierceTimeMinutes = (piercesCount * settings.PierceTimeSeconds) / 60.0;
                    double pierceCost = pierceTimeMinutes * machineCostPerHour;

                    double costPriceWithPierces = costPrice + pierceCost;

                    // Д. Цена для клиента = Себестоимость с наценкой (MarkupCoefficient задан в процентах).
                    // Интерпретируем значение как % наценки (например 150 => +150% => итог = cost * (1 + 150/100) = cost * 2.5).

                    // Добавляем наладку и минимальную цену за заказ
                    double priceForCutting = costPriceWithPierces * (1 + profile.MarkupCoefficient / 100.0);
                    priceForCutting += settings.LaserSetupCostPerJob / Math.Max(1, quantity); // распределяем наладку на детали
                    if (priceForCutting * quantity < settings.LaserMinChargePerJob)
                    {
                        // Если итог по заказу меньше минимума, выставляем минимум (распределяем на деталь)
                        priceForCutting = settings.LaserMinChargePerJob / (double)Math.Max(1, quantity);
                    }

                    // ✅ НОВЫЙ РАСЧЕТ ПРОБИВОК (цена за пробивку из профиля * количество пробивок на деталь)
                    double priceForPierces = profile.PiercePrice * piercesCount;

                    // Обновляем лог себестоимости с учётом пробивок
                    costPrice = costPriceWithPierces;

                    // Е. Сложность (Кувалда)
                    double handlingExtra = 0;
                    if (thicknessMm > settings.HeavyMaterialThresholdMm)
                        handlingExtra = settings.HeavyHandlingCostPerDetail;

                    // Итого за 1 деталь
                    double laserTotalPerOne = priceForCutting + priceForPierces + handlingExtra + (oxygenCost / quantity);

                    result.LaserCost = laserTotalPerOne * quantity;

                    // ✅ НОВОЕ: Детализация для отладки
                    result.LaserDetails =
                        $"cutLen={laserLengthMeters}m; speed={profile.CuttingSpeed}m/min; time={Math.Round(cuttingTimeMinutes,2)}min ({Math.Round(cuttingTimeHours,4)}h) | " +
                        $"machineRate={Math.Round(machineCostPerHour):N0}₸/h; baseCost={Math.Round(costPrice,2)}₸ | " +
                        $"cutPrice(one)={Math.Round(priceForCutting,2)}₸; pierce(one)={Math.Round(priceForPierces,2)}₸ ({piercesCount}шт) | " +
                        $"oxygenTotal={Math.Round(oxygenCost):N0}₸; handling(one)={Math.Round(handlingExtra,2)}₸";

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