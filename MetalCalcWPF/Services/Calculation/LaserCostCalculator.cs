using System;
using System.Linq;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.Services.Calculation
{
    /// <summary>
    /// Считает стоимость лазерной резки по Excel-модели (Спринт 2.2c) с учётом
    /// конкретного станка из справочника <see cref="CuttingMachine"/> (Спринт 2.2b).
    ///
    /// Паритет с листом «Лазер bodor» и формулой «Смета»!O3 из Excel-исходника цеха:
    ///
    ///   Себестоимость метра  = Цена минуты / Скорость резки
    ///   Цена клиенту за метр = Себестоимость метра × Коэффициент надбавки
    ///   Стоимость лазера     = длина × Цена клиенту за метр
    ///                        + кол-во врезок × Цена пробивки
    ///   Итого за партию      = (Стоимость лазера на 1 шт) × Количество
    ///                        + Setup (разово на партию)
    ///   и применяется пол MinCharge (разово на партию).
    ///
    /// Как подмешивается выбранный станок:
    /// 1. <see cref="CuttingMachine.PricePerMeterOverride"/> (если &gt; 0) — заменяет
    ///    «Цена клиенту за метр» напрямую, минуя формулу минута/скорость × K.
    ///    Применяется, когда у цеха есть прейскурант по договору с клиентом
    ///    или формула даёт нереалистичное число.
    /// 2. <see cref="CuttingMachine.SetupCostPerJob"/> — разовая плата за прогон
    ///    (наладка, подача, замена сопла). Добавляется ОДИН раз на всю партию,
    ///    не умножается на количество деталей.
    /// 3. <see cref="CuttingMachine.MinChargePerJob"/> — пол: если итог по партии
    ///    (рез + пробивки + setup) оказывается меньше — поднимаем до минимума.
    ///
    /// Выбор станка:
    /// • если в запросе задан <see cref="CalculationRequest.CuttingMachineId"/> — берём его;
    /// • иначе — первый активный станок типа <see cref="CuttingMachineKind.Laser"/>;
    /// • если станков в БД нет — работаем по чистой Excel-формуле, без добавок
    ///   (обратная совместимость со сценарием до 2.2b).
    ///
    /// Устаревшие поля <see cref="CuttingMachine"/> — <c>OperatorMonthlySalary</c>,
    /// <c>PowerConsumptionKw</c>, <c>AmortizationPerHour</c> — остались от старой
    /// декомпозиции до 2.2c и в расчёте больше не участвуют: зарплата/электричество
    /// /амортизация уже «зашиты» в <see cref="WorkshopSettings.GetLaserMinutePrice"/>.
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

            // Минут на метр = 1 / (м/мин). Например, 16мм на кислороде: 1/1.4 ≈ 0.7143.
            decimal minutesPerMeter = 1m / (decimal)profile.CuttingSpeed;

            decimal costPerMeter          = minutePrice * minutesPerMeter;                         // Лазер bodor F
            decimal formulaClientPerMeter = costPerMeter * (decimal)profile.MarkupCoefficient;     // Лазер bodor G

            // Подбираем станок: явно заданный, иначе первый активный лазер, иначе null.
            var machine = PickMachine(request.CuttingMachineId);
            bool overrideUsed = machine?.PricePerMeterOverride is { } o && o > 0m;
            decimal clientPerMeter = overrideUsed
                ? machine!.PricePerMeterOverride!.Value
                : formulaClientPerMeter;

            decimal cutChargePerOne    = clientPerMeter * (decimal)request.LaserLengthMeters;
            decimal pierceChargePerOne = (decimal)profile.PiercePrice * request.PiercesCount;

            decimal laserPerOne     = cutChargePerOne + pierceChargePerOne;
            decimal laserForQuantity = laserPerOne * request.Quantity;

            // Setup — один раз на всю партию.
            decimal setupCost = machine?.SetupCostPerJob ?? 0m;
            if (setupCost < 0m) setupCost = 0m;

            decimal subtotal = laserForQuantity + setupCost;

            // MinCharge — пол на всю партию.
            decimal minCharge = machine?.MinChargePerJob ?? 0m;
            bool minApplied = minCharge > 0m && subtotal < minCharge;
            decimal total = minApplied ? minCharge : subtotal;

            result.LaserCost = total;

            // Время реза (для отладки / лога — в цифровую цену больше не входит).
            double cuttingTimeMinutes = request.LaserLengthMeters / profile.CuttingSpeed;

            result.LaserDetails =
                $"machine={(machine?.Name ?? "—")}; " +
                $"len={request.LaserLengthMeters}m; thickness={request.ThicknessMm}mm; " +
                $"gas={(isAir ? "Air" : "O2")}; speed={profile.CuttingSpeed}m/min; time={Math.Round(cuttingTimeMinutes, 2)}min | " +
                $"minutePrice={minutePrice:N2} тг/мин; cost/m={costPerMeter:N2} тг; " +
                $"K={profile.MarkupCoefficient}; formulaClient/m={formulaClientPerMeter:N2} тг; " +
                $"client/m={clientPerMeter:N2} тг{(overrideUsed ? " (override)" : "")} | " +
                $"cut(one)={cutChargePerOne:N2} тг; pierce(one)={pierceChargePerOne:N2} тг ({request.PiercesCount}×{profile.PiercePrice}) | " +
                $"setup={setupCost:N2} тг; minCharge={minCharge:N2} тг{(minApplied ? " (applied)" : "")}; total={total:N2} тг";

            BuildBreakdown(request, profile, isAir, minutePrice, minutesPerMeter,
                costPerMeter, formulaClientPerMeter, clientPerMeter, cutChargePerOne,
                pierceChargePerOne, laserPerOne, laserForQuantity,
                machine, overrideUsed, setupCost, minCharge, minApplied, total, result);

            return $"+ Laser({request.PiercesCount}x pierce) ";
        }

        private CuttingMachine? PickMachine(int? explicitId)
        {
            try
            {
                if (explicitId is { } id)
                {
                    var byId = _db.GetCuttingMachineById(id);
                    if (byId != null && byId.IsActive) return byId;
                }

                // fallback: первый активный лазер из справочника
                return _db.GetCuttingMachinesByKind(CuttingMachineKind.Laser)
                          .FirstOrDefault(m => m.IsActive);
            }
            catch
            {
                // Если справочник станков по какой-то причине недоступен —
                // не срываем расчёт: работаем по чистой Excel-формуле.
                return null;
            }
        }

        private static void BuildBreakdown(
            CalculationRequest request, MaterialProfile profile, bool isAir,
            decimal minutePrice, decimal minutesPerMeter,
            decimal costPerMeter, decimal formulaClientPerMeter, decimal clientPerMeter,
            decimal cutChargePerOne, decimal pierceChargePerOne,
            decimal laserPerOne, decimal laserForQuantity,
            CuttingMachine? machine, bool overrideUsed,
            decimal setupCost, decimal minCharge, bool minApplied,
            decimal total, CalculationResult result)
        {
            var breakdown = new CalculationBreakdown
            {
                Section = "🔥 Лазер",
                Subtitle = $"{request.ThicknessMm}мм / {(isAir ? "воздух" : "кислород")} / " +
                           $"скорость {profile.CuttingSpeed} м/мин / K={profile.MarkupCoefficient}" +
                           (machine != null ? $" / станок: {machine.Name}" : string.Empty),
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

            if (overrideUsed)
            {
                // Показываем и «по формуле», и переопределение — чтобы было видно,
                // что станок перекрыл расчёт.
                breakdown.Lines.Add(new BreakdownLine(
                    "Цена по формуле за метр",
                    $"{costPerMeter:N2} × {profile.MarkupCoefficient} (K)",
                    Math.Round(formulaClientPerMeter, 2), "тг/м"));
                breakdown.Lines.Add(new BreakdownLine(
                    "Цена по станку (override)",
                    $"{machine!.Name} · PricePerMeterOverride",
                    Math.Round(clientPerMeter, 2), "тг/м"));
            }
            else
            {
                breakdown.Lines.Add(new BreakdownLine(
                    "Цена клиенту за метр",
                    $"{costPerMeter:N2} × {profile.MarkupCoefficient} (K)",
                    Math.Round(clientPerMeter, 2), "тг/м"));
            }

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
                    Math.Round(laserForQuantity, 2)));
            }

            if (setupCost > 0m)
            {
                breakdown.Lines.Add(new BreakdownLine(
                    "Наладка (setup)",
                    $"{machine!.Name} · SetupCostPerJob (разово)",
                    Math.Round(setupCost, 2)));
            }

            if (minApplied)
            {
                // Пол по min-charge сработал — показываем отдельной строкой.
                breakdown.Lines.Add(new BreakdownLine(
                    "Минимум за заказ",
                    $"{machine!.Name} · MinChargePerJob (пол)",
                    Math.Round(minCharge, 2)));
            }

            // Итоговая строка — то, что попадёт в LaserCost.
            string totalFormula;
            if (minApplied)
            {
                totalFormula = $"max({Math.Round(laserForQuantity + setupCost, 2)}, {Math.Round(minCharge, 2)})";
            }
            else if (setupCost > 0m)
            {
                totalFormula = request.Quantity > 1
                    ? $"{Math.Round(laserForQuantity, 2)} + {Math.Round(setupCost, 2)}"
                    : $"{Math.Round(laserPerOne, 2)} + {Math.Round(setupCost, 2)}";
            }
            else
            {
                totalFormula = request.Quantity > 1
                    ? $"{Math.Round(laserForQuantity, 2)}"
                    : $"{Math.Round(laserPerOne, 2)}";
            }
            breakdown.Lines.Add(new BreakdownLine(
                "Итого по лазеру",
                totalFormula,
                Math.Round(total, 2), "тг", IsTotal: true));

            result.Breakdowns.Add(breakdown);
        }
    }
}
