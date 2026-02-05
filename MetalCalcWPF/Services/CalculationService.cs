using System;
using MetalCalcWPF.Models;

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
    }

    public class CalculationService
    {
        private readonly DatabaseService _db;

        public CalculationService(DatabaseService db)
        {
            _db = db;
        }

        public CalculationResult CalculateOrder(
            double widthMm, double heightMm, double thicknessMm,
            int quantity,
            MaterialType material,
            double laserLengthMeters,
            bool useBending, int bendsCount, double bendLengthMm,
            bool useWelding, double weldLengthCm)
        {
            var result = new CalculationResult();
            var settings = _db.GetSettings();
            string logBuilder = "";

            // --- 1. МЕТАЛЛ (Material) ---
            if (widthMm > 0 && heightMm > 0 && material != null)
            {
                // Вес в кг
                double weightKg = (widthMm * heightMm * thicknessMm * material.Density) / 1_000_000.0;

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

                result.MaterialCost = weightKg * sellPricePerKg * quantity; // На всю партию
                logBuilder += $"Metal({Math.Round(weightKg, 1)}kg x {quantity}) ";
            }

            // --- 2. ЛАЗЕР (Laser) ---
            if (laserLengthMeters > 0)
            {
                var profile = _db.GetProfileByThickness(thicknessMm);
                if (profile != null)
                {
                    // А. Время резки (часы)
                    double cuttingTimeHours = (laserLengthMeters / profile.CuttingSpeed) / 60.0;

                    // Б. Стоимость часа работы станка (ЗП + Свет + Амортизация)
                    bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
                    double machineCostPerHour = settings.GetHourlyBaseCost(isAir);

                    if (!isAir) machineCostPerHour += settings.OxygenBottlePrice / 4.0; // Газ

                    // В. Себестоимость резки = Время * Тариф
                    double costPrice = cuttingTimeHours * machineCostPerHour;

                    // Г. Цена для клиента = Себестоимость * Наценку (Markup) + Пробивка
                    // MarkupCoefficient у нас большой (например 60), он работает как множитель
                    double priceForCutting = costPrice * profile.MarkupCoefficient;
                    double priceForPierce = profile.PiercePrice;

                    // Д. Сложность (Кувалда)
                    double handlingExtra = 0;
                    if (thicknessMm > settings.HeavyMaterialThresholdMm)
                        handlingExtra = settings.HeavyHandlingCostPerDetail;

                    // Итого за 1 деталь
                    double laserTotalPerOne = priceForCutting + priceForPierce + handlingExtra;

                    result.LaserCost = laserTotalPerOne * quantity;
                    logBuilder += "+ Laser ";
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
                    if (bendLengthMm <= 1500) pricePerBend = bendProfile.PriceLen1500;
                    else if (bendLengthMm <= 3000) pricePerBend = bendProfile.PriceLen3000;
                    else pricePerBend = bendProfile.PriceLen6000;

                    // Работа: Гибы * Цена * Кол-во деталей
                    double workCost = bendsCount * pricePerBend * quantity;

                    // Наладка: Одна на всю партию
                    double setupCost = bendProfile.SetupPrice;

                    bendPriceTotal = workCost + setupCost;
                    logBuilder += $"+ Bend({bendsCount}x) ";
                }
                else
                {
                    // Резерв
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

            result.Log = logBuilder;
            return result;
        }
    }
}