using MetalCalcWPF.Models;

namespace MetalCalcWPF.Services.Interfaces
{
    public interface ICalculationService
    {
        CalculationResult CalculateOrder(
            double widthMm, double heightMm, double thicknessMm,
            int quantity,
            MaterialType? material,
            double laserLengthMeters,
            int piercesCount,
            bool useBending, int bendsCount, double bendLengthMm,
            bool useWelding, double weldLengthCm,
            double measuredWeightKg = 0);
    }
}
