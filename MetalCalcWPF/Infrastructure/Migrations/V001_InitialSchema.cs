using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v1 — начальная схема БД. Все существующие таблицы на момент внедрения миграций.
    ///
    /// Использует CREATE TABLE IF NOT EXISTS, поэтому безопасно выполняется на
    /// существующих установках: если таблицы уже созданы старой версией приложения
    /// (когда-то через sqlite-net-pcl <c>CreateTable&lt;T&gt;</c>), миграция не делает
    /// ничего и просто регистрируется как применённая.
    ///
    /// <para>DDL написан вручную в стиле sqlite-net (типы <c>integer</c>, <c>decimal</c>,
    /// <c>float</c>, <c>varchar</c>, <c>bigint</c>, первичный ключ
    /// <c>integer primary key autoincrement not null</c>). Это сделано для
    /// совместимости: на существующей БД SQLite сравнивает текст DDL через
    /// <c>sqlite_master</c>, и абсолютная идентичность формулировок не требуется,
    /// но одинаковые имена типов делают диагностику и <c>.schema</c>-вывод
    /// предсказуемыми. <c>DateTime</c> хранится как <c>bigint</c> (ticks) —
    /// соответствует <c>StoreDateTimeAsTicks=true</c> у sqlite-net и
    /// <c>ValueConverter&lt;DateTime, long&gt;</c> в <see cref="AppDbContext"/>.</para>
    /// </summary>
    public class V001_InitialSchema : IMigration
    {
        public int Version => 1;

        public string Description => "Начальная схема: настройки, материалы, профили лазера/гибки/сварки, сортамент, история заказов.";

        public void Up(AppDbContext ctx)
        {
            ctx.Database.ExecuteSqlRaw(WorkshopSettingsDdl);
            ctx.Database.ExecuteSqlRaw(MaterialTypeDdl);
            ctx.Database.ExecuteSqlRaw(MaterialProfileDdl);
            ctx.Database.ExecuteSqlRaw(BendingProfileDdl);
            ctx.Database.ExecuteSqlRaw(WeldingProfileDdl);
            ctx.Database.ExecuteSqlRaw(RolledProfileDdl);
            ctx.Database.ExecuteSqlRaw(OrderHistoryDdl);
        }

        // ---- DDL всех таблиц v1 ------------------------------------------------
        //
        // Колонки перечислены в порядке, в котором их раньше генерировал
        // sqlite-net для соответствующего класса. Типы:
        //   int/long/bool/enum → integer
        //   double             → float
        //   decimal            → decimal
        //   string (не [NotNull]) → varchar (допускает NULL — для legacy-строк)
        //   DateTime (StoreDateTimeAsTicks=true) → bigint
        // Nullable<T> (int?, double?, decimal?) — без "not null".
        // PK int Id — "integer primary key autoincrement not null".

        private const string WorkshopSettingsDdl = @"
CREATE TABLE IF NOT EXISTS ""WorkshopSettings"" (
    ""Id"" integer primary key autoincrement not null,
    ""ElectricityPricePerKw"" decimal not null,
    ""OperatorMonthlySalary"" decimal not null,
    ""WorkDaysPerMonth"" integer not null,
    ""WorkHoursPerDay"" integer not null,
    ""OxygenBottlePrice"" decimal not null,
    ""AmortizationPerHour"" decimal not null,
    ""LaserSetupCostPerJob"" decimal not null,
    ""LaserMinChargePerJob"" decimal not null,
    ""PierceTimeSeconds"" float not null,
    ""LaserAirMinutePrice"" decimal not null,
    ""LaserOxygenMinutePrice"" decimal not null,
    ""OxygenBottleVolumeLiters"" float not null,
    ""OxygenBottlePressureAtm"" float not null,
    ""OxygenFlowRateLpm"" float not null,
    ""HeavyMaterialThresholdMm"" float not null,
    ""HeavyHandlingCostPerDetail"" decimal not null,
    ""LaserBasePowerConsumption"" float not null,
    ""CompressorIdlePower"" float not null,
    ""CompressorActivePower"" float not null,
    ""BendingOperatorSalary"" decimal not null,
    ""BendingMachinePower"" float not null,
    ""MaxBendingLengthMm"" float not null,
    ""BendingSetupPrice"" decimal not null,
    ""BendingBasePrice"" decimal not null,
    ""WelderMonthlySalary"" decimal not null,
    ""WeldingWirePricePerKg"" decimal not null,
    ""WeldingWireConsumptionGPerCm"" float not null,
    ""WeldingGasBottlePrice"" decimal not null,
    ""WeldingGasBottleVolumeLiters"" float not null,
    ""WeldingGasBottlePressureAtm"" float not null,
    ""WeldingGasFlowLpm"" float not null,
    ""WeldingConsumablesBudget"" decimal not null,
    ""WeldingMarkupCoefficient"" float not null,
    ""WeldingCostPerCm"" decimal not null,
    ""MaterialMarkupPercent"" decimal not null
)";

        private const string MaterialTypeDdl = @"
CREATE TABLE IF NOT EXISTS ""MaterialType"" (
    ""Id"" integer primary key autoincrement not null,
    ""Name"" varchar,
    ""Density"" float not null,
    ""BasePricePerKg"" decimal not null
)";

        private const string MaterialProfileDdl = @"
CREATE TABLE IF NOT EXISTS ""MaterialProfile"" (
    ""Id"" integer primary key autoincrement not null,
    ""Thickness"" float not null,
    ""GasType"" varchar,
    ""CuttingSpeed"" float not null,
    ""PiercePrice"" float not null,
    ""MarkupCoefficient"" float not null
)";

        private const string BendingProfileDdl = @"
CREATE TABLE IF NOT EXISTS ""BendingProfile"" (
    ""Id"" integer primary key autoincrement not null,
    ""Thickness"" float not null,
    ""V_Die"" float not null,
    ""MinFlange"" float not null,
    ""PriceLen1500"" float not null,
    ""PriceLen3000"" float not null,
    ""PriceLen6000"" float not null,
    ""SetupPrice"" float not null
)";

        private const string WeldingProfileDdl = @"
CREATE TABLE IF NOT EXISTS ""WeldingProfile"" (
    ""Id"" integer primary key autoincrement not null,
    ""FilletSize"" float not null,
    ""WeldingSpeed"" float not null,
    ""WeightPerCm"" float not null,
    ""CostPerCm"" decimal not null,
    ""PricePerCm"" decimal not null,
    ""MarkupCoefficient"" float not null
)";

        private const string RolledProfileDdl = @"
CREATE TABLE IF NOT EXISTS ""RolledProfile"" (
    ""Id"" integer primary key autoincrement not null,
    ""Kind"" integer not null,
    ""SizeCode"" varchar,
    ""GostDesignation"" varchar,
    ""WeightPerMeterKg"" float not null,
    ""Height"" float,
    ""Width"" float,
    ""WallThickness"" float,
    ""FlangeThickness"" float,
    ""OuterDiameter"" float,
    ""MaterialTypeId"" integer,
    ""PricePerMeterOverride"" decimal,
    ""CompatibleMachines"" integer not null,
    ""IsActive"" integer not null,
    ""Notes"" varchar
)";

        private const string OrderHistoryDdl = @"
CREATE TABLE IF NOT EXISTS ""OrderHistory"" (
    ""Id"" integer primary key autoincrement not null,
    ""CreatedDate"" bigint not null,
    ""ClientName"" varchar,
    ""Description"" varchar,
    ""TotalPrice"" decimal not null,
    ""OperationType"" varchar
)";
    }
}
