using System;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Считает стоимость лазерной резки по Excel-модели (Спринт 2.2c).
    ///
    /// Паритет с листом «Лазер bodor» и формулой «Смета»!O3 из
    /// рабочего Excel-исходника цеха:
    ///
    ///   Себестоимость метра  = Цена минуты / Скорость резки
    ///   Цена клиенту за метр = Себестоимость метра × Коэффициент надбавки
    ///   Стоимость лазера     = длина × Цена клиенту за метр
    ///                        + кол-во врезок × Цена пробивки
    ///   Итого за партию      = (Стоимость лазера на 1 шт) × Количество
    ///
    /// Ключевые отличия от старой модели:
    /// 1. «Коэффициент надбавки» — прямой множитель (×40), а не процент (+40%).
    ///    Старый код давал для 16мм × 1м ≈ 1211 тг вместо реальных 2428.57 тг.
    /// 2. Цена минуты — одно число из <see cref="WorkshopSettings"/>
    ///    (Air=65, Oxygen=85 тг/мин). Внутрь уже зашиты зарплата + электричество +
    ///    газ + расходники + амортизация — так же, как в Excel.
    /// 3. Пробивки добавляются к итогу «как есть» и не умножаются на коэффициент
    ///    (цена пробивки в профиле — это уже цена для клиента).
    /// 4. LaserSetupCostPerJob / LaserMinChargePerJob / HeavyHandling больше
    ///    не применяются в этом калькуляторе — их место по плану переедет в
    ///    будущую настройку по конкретному станку (Спринт 2.2b).
    ///
    /// НЕ менять формулы без согласования — напрямую влияют на КП клиенту.
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
            if (profile.CuttingSpeed <= 0) return string.Empty;

            bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
            decimal minutePrice = settings.GetLaserMinutePrice(isAir);        // Справочник!B9/B10

            // Минут на метр = 1 / (м/мин).  Например, 16мм на кислороде: 1/1.4 ≈ 0.7143.
            decimal minutesPerMeter = 1m / (decimal)profile.CuttingSpeed;

            decimal costPerMeter   = minutePrice * minutesPerMeter;                         // Лазер bodor F
            decimal clientPerMeter = costPerMeter * (decimal)profile.MarkupCoefficient;     // Лазер bodor G

            decimal cutChargePerOne    = clientPerMeter * (decimal)request.LaserLengthMeters;
            decimal pierceChargePerOne = (decimal)profile.PiercePrice * request.PiercesCount;

            decimal laserPerOne = cutChargePerOne + pierceChargePerOne;
            result.LaserCost = laserPerOne * request.Quantity;

            // Время реза (для отладки / лога — в цифровую цену больше не входит).
            double cuttingTimeMinutes = request.LaserLengthMeters / profile.CuttingSpeed;

            result.LaserDetails =
                $"len={request.LaserLengthMeters}m; thickness={request.ThicknessMm}mm; " +
                $"gas={(isAir ? "Air" : "O2")}; speed={profile.CuttingSpeed}m/min; time={Math.Round(cuttingTimeMinutes, 2)}min | " +
                $"minutePrice={minutePrice:N2} тг/мин; cost/m={costPerMeter:N2} тг; " +
                $"K={profile.MarkupCoefficient}; client/m={clientPerMeter:N2} тг | " +
                $"cut(one)={cutChargePerOne:N2} тг; pierce(one)={pierceChargePerOne:N2} тг ({request.PiercesCount}×{profile.PiercePrice})";

            // Прозрачная детализация расчёта (для UI-блока «🔍 Детализация расчёта»).
            // Порядок строк = порядок формул из Excel «Лазер bodor» сверху вниз,
            // чтобы оператор мог сверить каждую цифру с таблицей цеха.
            var breakdown = new CalculationBreakdown
            {
                Section = "🔥 Лазер",
                Subtitle = $"{request.ThicknessMm}мм / {(isAir ? "воздух" : "кислород")} / " +
                           $"скорость {profile.CuttingSpeed} м/мин / K={profile.MarkupCoefficient}",
            };
            breakdown.Lines.Add(new BreakdownLine(
                "Цена минуты",
                isAir ? "Справочник B9 (воздух)" : "Справочник B10 (кислород)",
                minutePrice, "тг/мин"));
            breakdown.Lines.Add(new BreakdownLine(
                "Минут на метр",
                $"1 / {profile.CuttingSpeed} м/мин",
                Math.Round(minutesPerMeter, 4), "мин/м"));
            breakdown.Lines.Add(new BreakdownLine(
                "Себестоимость метра",
                $"{minutePrice:N2} × {minutesPerMeter:N4}",
                Math.Round(costPerMeter, 2), "тг/м"));
            breakdown.Lines.Add(new BreakdownLine(
                "Цена клиенту за метр",
                $"{costPerMeter:N2} × {profile.MarkupCoefficient} (K)",
                Math.Round(clientPerMeter, 2), "тг/м"));
            breakdown.Lines.Add(new BreakdownLine(
                "Стоимость реза (1 шт)",
                $"{clientPerMeter:N2} × {request.LaserLengthMeters} м",
                Math.Round(cutChargePerOne, 2)));
            if (request.PiercesCount > 0)
            {
                breakdown.Lines.Add(new BreakdownLine(
                    "Пробивки (1 шт)",
                    $"{request.PiercesCount} × {profile.PiercePrice} тг",
                    Math.Round(pierceChargePerOne, 2)));
            }
            breakdown.Lines.Add(new BreakdownLine(
                "Итого за 1 шт",
                request.PiercesCount > 0
                    ? $"{cutChargePerOne:N2} + {pierceChargePerOne:N2}"
                    : $"{cutChargePerOne:N2}",
                Math.Round(laserPerOne, 2)));
            if (request.Quantity > 1)
            {
                breakdown.Lines.Add(new BreakdownLine(
                    $"Итого за {request.Quantity} шт",
                    $"{laserPerOne:N2} × {request.Quantity}",
                    Math.Round(result.LaserCost, 2), "тг", IsTotal: true));
            }
            else
            {
                breakdown.Lines[^1] = breakdown.Lines[^1] with { IsTotal = true };
            }
            result.Breakdowns.Add(breakdown);

            return $"+ Laser({request.PiercesCount}x pierce) ";
        }
    }
}
