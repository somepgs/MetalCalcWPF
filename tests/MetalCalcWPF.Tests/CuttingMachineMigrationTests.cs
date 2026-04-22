using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF.Infrastructure.Migrations;
using MetalCalcWPF.Infrastructure.Persistence;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты миграции v2 (добавление таблицы станков резки) и базовой целостности.
    /// Все сценарии работают с in-memory SQLite через <see cref="AppDbContext"/>.
    /// </summary>
    [TestClass]
    public class CuttingMachineMigrationTests
    {
        private static (SqliteConnection conn, AppDbContext ctx) CreateInMemory()
        {
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(conn)
                .Options;
            return (conn, new AppDbContext(options));
        }

        [TestMethod]
        public void V002_CreatesCuttingMachineTable_AndSeedsDefaultLaser()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                // V001 обязана отработать первой, иначе таблицы settings не будет,
                // и сид v2 не сможет прочитать текущие параметры.
                var applied = MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V002_AddCuttingMachines(),
                });

                CollectionAssert.AreEqual(new[] { 1, 2 }, applied);

                var machines = ctx.CuttingMachines.AsNoTracking().ToList();
                Assert.AreEqual(1, machines.Count,
                    "V002 должна засеять ровно один дефолтный лазер на чистой БД");

                var laser = machines[0];
                Assert.AreEqual(CuttingMachineKind.Laser, laser.Kind);
                Assert.IsTrue(laser.IsActive);
                Assert.IsFalse(string.IsNullOrWhiteSpace(laser.Name),
                    "Имя дефолтного станка не должно быть пустым");
            }
        }

        [TestMethod]
        public void V002_SeedsFromExistingSettings_IfSettingsRowPresent()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                // Эмулируем «продовую» ситуацию: v1 уже применена, пользователь
                // поменял параметры цеха, теперь применяем v2.
                MigrationRunner.Run(ctx, new IMigration[] { new V001_InitialSchema() });

                ctx.WorkshopSettings.Add(new WorkshopSettings
                {
                    OperatorMonthlySalary = 777777m,
                    LaserBasePowerConsumption = 30.0,
                    CompressorActivePower = 20.0,
                    AmortizationPerHour = 1234m,
                    LaserSetupCostPerJob = 2500m,
                    LaserMinChargePerJob = 1500m,
                });
                ctx.SaveChanges();

                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V002_AddCuttingMachines(),
                });

                var laser = ctx.CuttingMachines.AsNoTracking().First();
                Assert.AreEqual(777777m, laser.OperatorMonthlySalary,
                    "Зарплата должна быть взята из текущих настроек");
                Assert.AreEqual(50.0, laser.PowerConsumptionKw, 0.001,
                    "Мощность = Base(30) + CompressorActive(20) = 50 кВт");
                Assert.AreEqual(1234m, laser.AmortizationPerHour);
                Assert.AreEqual(2500m, laser.SetupCostPerJob);
                Assert.AreEqual(1500m, laser.MinChargePerJob);
                Assert.IsNull(laser.PricePerMeterOverride,
                    "По умолчанию override цены за метр не задан");
            }
        }

        [TestMethod]
        public void V002_IsIdempotent_DoesNotDoubleSeed()
        {
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V002_AddCuttingMachines(),
                });

                // Повторный прогон — не должен ни дублировать станок, ни бросать исключение.
                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V002_AddCuttingMachines(),
                });

                Assert.AreEqual(1, ctx.CuttingMachines.AsNoTracking().Count(),
                    "Повторный прогон не должен дублировать дефолтный станок");
            }
        }

        [TestMethod]
        public void V002_OnExistingCuttingMachineTable_DoesNotSeed()
        {
            // Сценарий «пользователь уже заводил станки вручную» — v2 не должна
            // затирать или дублировать их.
            var (conn, ctx) = CreateInMemory();
            using (conn) using (ctx)
            {
                MigrationRunner.Run(ctx, new IMigration[] { new V001_InitialSchema() });

                // Создаём таблицу и вставляем «старый» станок заранее — как если бы
                // пользователь вручную завёл его до применения v2.
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
                ctx.CuttingMachines.Add(new CuttingMachine { Name = "Мой старый лазер", Kind = CuttingMachineKind.Laser });
                ctx.SaveChanges();

                MigrationRunner.Run(ctx, new IMigration[]
                {
                    new V001_InitialSchema(),
                    new V002_AddCuttingMachines(),
                });

                var machines = ctx.CuttingMachines.AsNoTracking().ToList();
                Assert.AreEqual(1, machines.Count, "Сид v2 не должен добавлять своё, если пользовательские записи уже есть");
                Assert.AreEqual("Мой старый лазер", machines[0].Name);
            }
        }

        [TestMethod]
        public void GetAllMigrations_IncludesV002InOrder()
        {
            var all = MigrationRunner.GetAllMigrations();
            var versions = all.Select(m => m.Version).ToList();
            CollectionAssert.Contains(versions, 2, "Канонический список должен содержать V002");
            Assert.IsTrue(versions.IndexOf(1) < versions.IndexOf(2),
                "V001 должна идти до V002 в списке");
        }
    }
}
