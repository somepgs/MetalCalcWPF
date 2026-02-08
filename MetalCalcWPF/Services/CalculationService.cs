using System;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services
{
    // Класс для результата (Чек)
    public class CalculationResult
    {
        public decimal MaterialCost { get; set; }  // Цена металла
        public decimal LaserCost { get; set; }     // Цена резки
        public decimal BendingCost { get; set; }   // Цена гибки
        public decimal WeldingCost { get; set; }   // Цена сварки

        public decimal TotalPrice => MaterialCost + LaserCost + BendingCost + WeldingCost;

        public string Log { get; set; }           // История (что посчитали)

        // ✅ НОВОЕ: Детализация расчета лазера
        public string LaserDetails { get; set; }  // Подробности расчета лазера
    
    }

    // CalculationService implements the ICalculationService defined in Services.Interfaces
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
                decimal costPricePerKg = material.BasePricePerKg;

                // Цена продажи = Закуп * (1 + Наценка%)
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
                    decimal machineCostPerHour = settings.GetHourlyBaseCost(isAir);

                    // ✅ НОВЫЙ РАСЧЕТ КИСЛОРОДА (ТОЧНЫЙ) — используем метод настроек
                    decimal oxygenCost = 0m;
                    if (!isAir)
                    {
                        decimal oxygenCostPerMinute = settings.GetOxygenCostPerMinute();
                        oxygenCost = oxygenCostPerMinute * (decimal)cuttingTimeMinutes * quantity;
                    }

                    // Г. Себестоимость резки = Время * Тариф
                    decimal costPrice = (decimal)cuttingTimeHours * machineCostPerHour;

                    // Учитываем время пробивок в себестоимости: пробивки занимают время и потребляют ресурс
                    double pierceTimeMinutes = (piercesCount * settings.PierceTimeSeconds) / 60.0;
                    decimal pierceCost = (decimal)pierceTimeMinutes * machineCostPerHour;

                    decimal costPriceWithPierces = costPrice + pierceCost;

                    // Д. Цена для клиента = Себестоимость с наценкой (MarkupCoefficient задан в процентах).
                    // Интерпретируем значение как % наценки (например 150 => +150% => итог = cost * (1 + 150/100) = cost * 2.5).

                    // Добавляем наладку и минимальную цену за заказ
                    decimal priceForCutting = costPriceWithPierces * (1 + (decimal)profile.MarkupCoefficient / 100m);
                    priceForCutting += settings.LaserSetupCostPerJob / Math.Max(1, quantity); // распределяем наладку на детали
                    if (priceForCutting * quantity < settings.LaserMinChargePerJob)
                    {
                        // Если итог по заказу меньше минимума, выставляем минимум (распределяем на деталь)
                        priceForCutting = settings.LaserMinChargePerJob / (decimal)Math.Max(1, quantity);
                    }

                    // ✅ НОВЫЙ РАСЧЕТ ПРОБИВОК (цена за пробивку из профиля * количество пробивок на деталь)
                    decimal priceForPierces = (decimal)profile.PiercePrice * piercesCount;

                    // Обновляем лог себестоимости с учётом пробивок
                    costPrice = costPriceWithPierces;

                    // Е. Сложность (Кувалда)
                    decimal handlingExtra = 0m;
                    if (thicknessMm > settings.HeavyMaterialThresholdMm)
                        handlingExtra = settings.HeavyHandlingCostPerDetail;

                    // Итого за 1 деталь
                    decimal laserTotalPerOne = priceForCutting + priceForPierces + handlingExtra + (oxygenCost / Math.Max(1, quantity));

                    result.LaserCost = laserTotalPerOne * quantity;

                    // ✅ НОВОЕ: Детализация для отладки
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
                    // Цена за 1 гиб (по длине)
                    decimal pricePerBend = 0m;
                    if (bendLengthMm <= 1500)
                        pricePerBend = (decimal)bendProfile.PriceLen1500;
                    else if (bendLengthMm <= 3000)
                        pricePerBend = (decimal)bendProfile.PriceLen3000;
                    else
                        pricePerBend = (decimal)bendProfile.PriceLen6000;

                    // Работа: Гибы * Цена * Кол-во деталей
                    decimal workCost = bendsCount * pricePerBend * quantity;

                    // Наладка: Одна на всю партию
                    decimal setupCost = (decimal)bendProfile.SetupPrice;

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
                decimal weldTotal = (decimal)weldLengthCm * settings.WeldingCostPerCm * quantity;
                result.WeldingCost = weldTotal;
                logBuilder += $"+ Weld({weldLengthCm}cm) ";
            }

            result.Log = logBuilder.Trim();
            return result;
        }
    }
}