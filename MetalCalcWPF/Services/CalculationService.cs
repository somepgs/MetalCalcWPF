using System.Collections.Generic;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Calculation;
using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.Services.Logging;

namespace MetalCalcWPF.Services
{
    /// <summary>
    /// Сводный результат калькуляции одного заказа.
    ///
    /// Расшифровка строковых полей:
    /// <list type="bullet">
    ///   <item><see cref="Log"/> — короткая сводка ("Metal(...) + Laser(5x pierce) + Bend(2x) + Weld(50cm, 5mm)"),
    ///     собирается оркестратором из фрагментов калькуляторов.</item>
    ///   <item><see cref="LaserDetails"/> — полная детализация расчёта лазера (для UI/экспорта).</item>
    ///   <item><see cref="WeldingDetails"/> — полная детализация расчёта сварки.</item>
    /// </list>
    /// </summary>
    public class CalculationResult
    {
        public decimal MaterialCost { get; set; }
        public decimal LaserCost { get; set; }
        public decimal BendingCost { get; set; }
        public decimal WeldingCost { get; set; }

        public decimal TotalPrice => MaterialCost + LaserCost + BendingCost + WeldingCost;

        public string Log { get; set; } = string.Empty;
        public string LaserDetails { get; set; } = string.Empty;
        public string WeldingDetails { get; set; } = string.Empty;
    }

    /// <summary>
    /// Оркестратор калькуляции заказа.
    ///
    /// Не содержит бизнес-формул — только:
    /// 1) собирает <see cref="CalculationRequest"/> из аргументов метода;
    /// 2) запрашивает актуальные <see cref="WorkshopSettings"/> у БД;
    /// 3) последовательно прогоняет список <see cref="IOperationCalculator"/>;
    /// 4) собирает сводный лог из фрагментов и возвращает <see cref="CalculationResult"/>.
    ///
    /// ВАЖНО: чтобы добавить новую операцию (например, «Покраска»):
    /// 1) добавь поля в <see cref="CalculationResult"/> и <see cref="CalculationRequest"/>;
    /// 2) создай Paint­Cost­Calculator : <see cref="IOperationCalculator"/>;
    /// 3) добавь его в список <see cref="BuildPipeline"/>;
    /// 4) ничего в этом файле больше править не надо.
    /// </summary>
    public class CalculationService : ICalculationService
    {
        private readonly IDatabaseService _db;
        private readonly IAppLogger _log;
        private readonly IReadOnlyList<IOperationCalculator> _pipeline;

        public CalculationService(IDatabaseService db) : this(db, NullAppLogger.Instance)
        {
        }

        public CalculationService(IDatabaseService db, IAppLogger log)
        {
            _db = db;
            _log = log ?? NullAppLogger.Instance;
            _pipeline = BuildPipeline(db);
        }

        private static IReadOnlyList<IOperationCalculator> BuildPipeline(IDatabaseService db) => new IOperationCalculator[]
        {
            new MaterialCostCalculator(),
            new LaserCostCalculator(db),
            new BendingCostCalculator(db),
            new WeldingCostCalculator(db),
        };

        /// <summary>
        /// Главный метод расчёта стоимости заказа.
        /// Сигнатура сохранена из v1 ради совместимости с вызывающим кодом (MainViewModel)
        /// и тестами — менять нельзя без обсуждения.
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
            var request = new CalculationRequest
            {
                WidthMm = widthMm,
                HeightMm = heightMm,
                ThicknessMm = thicknessMm,
                Quantity = quantity,
                Material = material,
                LaserLengthMeters = laserLengthMeters,
                PiercesCount = piercesCount,
                UseBending = useBending,
                BendsCount = bendsCount,
                BendLengthMm = bendLengthMm,
                UseWelding = useWelding,
                WeldLengthCm = weldLengthCm,
                MeasuredWeightKg = measuredWeightKg,
            };

            var settings = _db.GetSettings();
            var result = new CalculationResult();
            var logBuilder = new System.Text.StringBuilder();

            foreach (var calculator in _pipeline)
            {
                try
                {
                    var fragment = calculator.Apply(request, settings, result);
                    if (!string.IsNullOrEmpty(fragment))
                        logBuilder.Append(fragment);
                }
                catch (System.Exception ex)
                {
                    // Одна операция упала — не убиваем весь расчёт, пишем в лог и идём дальше.
                    // Проблемная операция просто не внесёт вклад в стоимость.
                    _log.Exception(ex, "Операция {0} упала во время расчёта", calculator.Name);
                }
            }

            result.Log = logBuilder.ToString().Trim();

            _log.Info("Расчёт заказа: кол-во={0}, толщ={1}мм, laser={2}м, bends={3}, weld={4}см → Mat={5} Laser={6} Bend={7} Weld={8} Итого={9} тг",
                request.Quantity, request.ThicknessMm, request.LaserLengthMeters,
                request.UseBending ? request.BendsCount : 0,
                request.UseWelding ? request.WeldLengthCm : 0,
                System.Math.Round(result.MaterialCost),
                System.Math.Round(result.LaserCost),
                System.Math.Round(result.BendingCost),
                System.Math.Round(result.WeldingCost),
                System.Math.Round(result.TotalPrice));

            return result;
        }
    }
}
