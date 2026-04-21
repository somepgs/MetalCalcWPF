using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SQLite;
using MetalCalcWPF.Infrastructure.Migrations;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты миграции v2 (добавление таблицы станков резки) и базовой целостности.
    /// Все сценарии работают с in-memory SQLite.
    /// </summary>
    [TestClass]
    public class CuttingMachineMigrationTests
    {
        [TestMethod]
        public void V002_CreatesCuttingMachineTable_AndSeedsDefaultLaser()
        {
            using var db = new SQLiteConnection(":memory:");

            // V001 обязана отработать первой, иначе таблицы settings не будет,
            // и сид v2 не сможет прочитать текущие параметры.
            var applied = MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V002_AddCuttingMachines(),
            });

            CollectionAssert.AreEqual(new[] { 1, 2 }, applied);

            var machines = db.Table<CuttingMachine>().ToList();
            Assert.AreEqual(1, machines.Count,
                "V002 должна засеять ровно один дефолтный лазер на чистой БД");

            var laser = machines[0];
            Assert.AreEqual(CuttingMachineKind.Laser, laser.Kind);
            Assert.IsTrue(laser.IsActive);
            Assert.IsFalse(string.IsNullOrWhiteSpace(laser.Name),
                "Имя дефолтного станка не должно быть пустым");
        }

        [TestMethod]
        public void V002_SeedsFromExistingSettings_IfSettingsRowPresent()
        {
            using var db = new SQLiteConnection(":memory:");

            // Эмулируем «продовую» ситуацию: v1 уже применена, пользователь
            // поменял параметры цеха, теперь применяем v2.
            MigrationRunner.Run(db, new IMigration[] { new V001_InitialSchema() });

            db.Insert(new WorkshopSettings
            {
                OperatorMonthlySalary = 777777m,
                LaserBasePowerConsumption = 30.0,
                CompressorActivePower = 20.0,
                AmortizationPerHour = 1234m,
                LaserSetupCostPerJob = 2500m,
                LaserMinChargePerJob = 1500m,
            });

            MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V002_AddCuttingMachines(),
            });

            var laser = db.Table<CuttingMachine>().First();
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

        [TestMethod]
        public void V002_IsIdempotent_DoesNotDoubleSeed()
        {
            using var db = new SQLiteConnection(":memory:");

            MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V002_AddCuttingMachines(),
            });

            // Повторный прогон — не должен ни дублировать станок, ни бросать исключение.
            MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V002_AddCuttingMachines(),
            });

            Assert.AreEqual(1, db.Table<CuttingMachine>().Count(),
                "Повторный прогон не должен дублировать дефолтный станок");
        }

        [TestMethod]
        public void V002_OnExistingCuttingMachineTable_DoesNotSeed()
        {
            // Сценарий «пользователь уже заводил станки вручную» — v2 не должна
            // затирать или дублировать их.
            using var db = new SQLiteConnection(":memory:");

            MigrationRunner.Run(db, new IMigration[] { new V001_InitialSchema() });
            db.CreateTable<CuttingMachine>();
            db.Insert(new CuttingMachine { Name = "Мой старый лазер", Kind = CuttingMachineKind.Laser });

            // Теперь применяем v2 — таблица уже есть, в ней есть запись.
            MigrationRunner.Run(db, new IMigration[]
            {
                new V001_InitialSchema(),
                new V002_AddCuttingMachines(),
            });

            var machines = db.Table<CuttingMachine>().ToList();
            Assert.AreEqual(1, machines.Count, "Сид v2 не должен добавлять своё, если пользовательские записи уже есть");
            Assert.AreEqual("Мой старый лазер", machines[0].Name);
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
