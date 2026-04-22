using System.Linq;
using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v2 — таблица станков резки (<see cref="CuttingMachine"/>).
    ///
    /// Что делает:
    /// 1) Создаёт таблицу CuttingMachine (CREATE TABLE IF NOT EXISTS).
    /// 2) Если таблица пустая — засевает одну дефолтную запись «Лазер по умолчанию»
    ///    с параметрами, взятыми из текущих <see cref="WorkshopSettings"/>.
    ///    Это гарантирует, что после миграции у пользователя уже есть валидный
    ///    лазер в справочнике и он не остаётся пустым на этапе Спринта 2.2a.
    ///
    /// ПРОДЫ с уже созданным «сортаментом проката» получат v2 без побочных
    /// эффектов: таблицы сортамента мы не трогаем, расчёт сметы пока тоже.
    /// </summary>
    public class V002_AddCuttingMachines : IMigration
    {
        public int Version => 2;

        public string Description => "Добавлена таблица станков резки (CuttingMachine).";

        public void Up(AppDbContext ctx)
        {
            // DDL в стиле sqlite-net CreateTable<CuttingMachine>.
            ctx.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""CuttingMachine"" (
    ""Id"" integer primary key autoincrement not null,
    ""Name"" varchar,
    ""Kind"" integer not null,
    ""OperatorMonthlySalary"" decimal not null,
    ""PowerConsumptionKw"" float not null,
    ""AmortizationPerHour"" decimal not null,
    ""SetupCostPerJob"" decimal not null,
    ""MinChargePerJob"" decimal not null,
    ""PricePerMeterOverride"" decimal,
    ""IsActive"" integer not null,
    ""Notes"" varchar
)");

            // Если в таблице уже есть записи — не дублируем (сценарий «пользователь
            // уже заводил станки вручную» или повторный прогон после краша).
            if (ctx.CuttingMachines.AsNoTracking().Any())
                return;

            // Забираем текущие «лазерные» параметры из settings — если они есть,
            // иначе используем разумные дефолты из WorkshopSettings.
            var settings = ctx.WorkshopSettings.AsNoTracking().FirstOrDefault() ?? new WorkshopSettings();

            ctx.CuttingMachines.Add(new CuttingMachine
            {
                Name = "Лазер (по умолчанию)",
                Kind = CuttingMachineKind.Laser,
                OperatorMonthlySalary = settings.OperatorMonthlySalary,
                // Суммарная мощность лазера в «рабочем режиме» ≈ база + активный компрессор,
                // чтобы ставка в кВт·ч совпадала с той, что даёт GetHourlyBaseCost(true).
                PowerConsumptionKw = settings.LaserBasePowerConsumption + settings.CompressorActivePower,
                AmortizationPerHour = settings.AmortizationPerHour,
                SetupCostPerJob = settings.LaserSetupCostPerJob,
                MinChargePerJob = settings.LaserMinChargePerJob,
                PricePerMeterOverride = null,
                IsActive = true,
                Notes = "Создан автоматически миграцией v2 из текущих настроек цеха."
            });
            ctx.SaveChanges();
        }
    }
}
