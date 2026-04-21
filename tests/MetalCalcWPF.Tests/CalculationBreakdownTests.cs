using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services;
using MetalCalcWPF.Services.Calculation;
using MetalCalcWPF.Services.Interfaces;
using System.Collections.Generic;

namespace MetalCalcWPF.Tests
{
    /// <summary>
    /// Тесты «прозрачной детализации» расчёта (Спринт 2.2d).
    /// Проверяем, что каждый калькулятор пишет понятные шаги формулы
    /// в <see cref="CalculationResult.Breakdowns"/> и что ключевые числа
    /// в этих шагах совпадают с реальными промежуточными значениями Excel-модели.
    /// </summary>
    [TestClass]
    public class CalculationBreakdownTests
    {
        private class FakeDb : IDatabaseService
        {
            public WorkshopSettings Settings { get; set; } = new WorkshopSettings();
            public List<MaterialProfile> Profiles { get; set; } = new List<MaterialProfile>();

            public WorkshopSettings GetSettings() => Settings;
            public void SaveSettings(WorkshopSettings settings) { Settings = settings; }
            public MaterialProfile? GetProfileByThickness(double thickness)
            {
                foreach (var p in Profiles) if (p.Thickness >= thickness) return p;
                return null;
            }
            public BendingProfile? GetBendingProfile(double thickness) => null;
            public WeldingProfile? GetWeldingProfile(double thickness) => null;
            public void SaveOrder(OrderHistory order) { }
            public void DeleteOrder(int id) { }
            public List<OrderHistory> GetRecentOrders() => new List<OrderHistory>();
            public List<MaterialType> GetMaterials() => new List<MaterialType>();
            public void UpdateAllMaterials(List<MaterialType> list) { }
            public List<MaterialProfile> GetAllLaserProfiles() => Profiles;
            public void UpdateAllLaserProfiles(List<MaterialProfile> list) { Profiles = list; }
            public List<BendingProfile> GetAllBendingProfiles() => new List<BendingProfile>();
            public void UpdateAllBendingProfiles(List<BendingProfile> list) { }
            public List<WeldingProfile> GetAllWeldingProfiles() => new List<WeldingProfile>();
            public void UpdateAllWeldingProfiles(List<WeldingProfile> list) { }
            public List<RolledProfile> RolledProfiles { get; set; } = new List<RolledProfile>();
            public List<RolledProfile> GetAllRolledProfiles() => RolledProfiles;
            public List<RolledProfile> GetRolledProfilesByKind(ProfileKind kind) => new List<RolledProfile>();
            public RolledProfile? GetRolledProfileById(int id) => null;
            public int AddRolledProfile(RolledProfile profile) => 0;
            public void UpdateRolledProfile(RolledProfile profile) { }
            public void DeleteRolledProfile(int id) { }
            public void UpdateAllRolledProfiles(List<RolledProfile> list) { }
            public List<CuttingMachine> CuttingMachines { get; set; } = new List<CuttingMachine>();
            public List<CuttingMachine> GetAllCuttingMachines() => CuttingMachines;
            public List<CuttingMachine> GetCuttingMachinesByKind(CuttingMachineKind kind) => new List<CuttingMachine>();
            public CuttingMachine? GetCuttingMachineById(int id) => null;
            public int AddCuttingMachine(CuttingMachine machine) => 0;
            public void UpdateCuttingMachine(CuttingMachine machine) { }
            public void DeleteCuttingMachine(int id) { }
            public void UpdateAllCuttingMachines(List<CuttingMachine> list) { }
        }

        /// <summary>
        /// Для эталонного Excel-кейса (16мм × 1м, кислород, K=40, минута 85 тг)
        /// детализация лазера должна содержать ключевые шаги и итог 2428.57 тг.
        /// </summary>
        [TestMethod]
        public void LaserBreakdown_16mm_1m_ContainsKeyStepsAndExcelTotal()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 16, GasType = "Oxygen",
                CuttingSpeed = 1.4, PiercePrice = 130, MarkupCoefficient = 40,
            });
            db.Settings.LaserOxygenMinutePrice = 85m;
            db.Settings.MaterialMarkupPercent = 0;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 16, 1, mat,
                laserLengthMeters: 1.0, piercesCount: 0,
                useBending: false, bendsCount: 0, bendLengthMm: 0,
                useWelding: false, weldLengthCm: 0);

            var laser = r.Breakdowns.FirstOrDefault(b => b.Section.Contains("Лазер"));
            Assert.IsNotNull(laser, "В детализации должна быть секция «Лазер»");

            var labels = laser!.Lines.Select(l => l.Label).ToList();
            CollectionAssert.Contains(labels, "Цена минуты");
            CollectionAssert.Contains(labels, "Минут на метр");
            CollectionAssert.Contains(labels, "Себестоимость метра");
            CollectionAssert.Contains(labels, "Цена клиенту за метр");

            // Себестоимость метра (Excel: 85/1.4 ≈ 60.71)
            var costPerMeter = laser.Lines.First(l => l.Label == "Себестоимость метра");
            Assert.AreEqual(60.71m, costPerMeter.Value, "cost/m должна быть ≈ 60.71 тг");

            // Цена клиенту за метр (Excel: 60.71 × 40 ≈ 2428.57)
            var clientPerMeter = laser.Lines.First(l => l.Label == "Цена клиенту за метр");
            Assert.AreEqual(2428.57m, clientPerMeter.Value, "client/m должна быть ≈ 2428.57 тг");

            // Должен быть ровно один итоговый шаг, его Value = 2428.57
            var totals = laser.Lines.Where(l => l.IsTotal).ToList();
            Assert.AreEqual(1, totals.Count, "Должен быть ровно один «итоговый» шаг");
            Assert.AreEqual(2428.57m, totals[0].Value);
        }

        /// <summary>
        /// Когда в заказе есть пробивки, в детализации должна появиться отдельная строка
        /// «Пробивки (1 шт)» с корректным значением = PiercePrice × count.
        /// </summary>
        [TestMethod]
        public void LaserBreakdown_WithPierces_ShowsPierceLine()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 5, GasType = "Air",
                CuttingSpeed = 10.0, PiercePrice = 100, MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;
            db.Settings.MaterialMarkupPercent = 0;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 5, 1, mat, 1.0, 3, false, 0, 0, false, 0);

            var laser = r.Breakdowns.First(b => b.Section.Contains("Лазер"));
            var pierce = laser.Lines.First(l => l.Label == "Пробивки (1 шт)");
            Assert.AreEqual(300m, pierce.Value, "3 × 100 = 300 тг за пробивки");
        }

        /// <summary>
        /// Для партии &gt; 1 шт должна появиться строка «Итого за N шт», умноженная на количество.
        /// </summary>
        [TestMethod]
        public void LaserBreakdown_MultipleQuantity_ShowsBatchTotal()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 10, GasType = "Air",
                CuttingSpeed = 10.0, PiercePrice = 0, MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;
            db.Settings.MaterialMarkupPercent = 0;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 10, 5, mat, 1.0, 0, false, 0, 0, false, 0);

            var laser = r.Breakdowns.First(b => b.Section.Contains("Лазер"));
            var batch = laser.Lines.First(l => l.Label == "Итого за 5 шт");
            Assert.AreEqual(1000m, batch.Value, "1 шт = 200 тг; 5 шт = 1000 тг");
            Assert.IsTrue(batch.IsTotal, "Именно строка по партии должна быть помечена как итоговая");
        }

        /// <summary>
        /// Сварочный fallback (нет профиля по толщине) должен всё равно выдавать
        /// понятную детализацию из двух строк.
        /// </summary>
        [TestMethod]
        public void WeldingBreakdown_FallbackBasic_ShowsPricePerCmAndTotal()
        {
            var db = new FakeDb();
            // Профиля нет (FakeDb.GetWeldingProfile возвращает null) — сработает fallback
            db.Settings.WeldingCostPerCm = 50m;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 3, 2, mat, 0, 0, false, 0, 0,
                useWelding: true, weldLengthCm: 20);

            var weld = r.Breakdowns.FirstOrDefault(b => b.Section.Contains("Сварка"));
            Assert.IsNotNull(weld, "Должна быть секция «Сварка»");

            var total = weld!.Lines.First(l => l.IsTotal);
            // 20 см × 50 тг/см × 2 шт = 2000 тг
            Assert.AreEqual(2000m, total.Value);
        }

        /// <summary>
        /// Если заказ — только металл (без лазера/гибки/сварки), в детализации
        /// должна быть ровно одна секция «Металл».
        /// </summary>
        [TestMethod]
        public void MaterialOnly_ProducesSingleBreakdownSection()
        {
            var db = new FakeDb();
            db.Settings.MaterialMarkupPercent = 0;
            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 500 };

            var r = svc.CalculateOrder(0, 0, 5, 1, mat, 0, 0, false, 0, 0, false, 0, measuredWeightKg: 10);

            Assert.AreEqual(1, r.Breakdowns.Count);
            Assert.IsTrue(r.Breakdowns[0].Section.Contains("Металл"));
            var total = r.Breakdowns[0].Lines.First(l => l.IsTotal);
            Assert.AreEqual(5000m, total.Value, "10 кг × 500 тг/кг = 5000 тг");
        }
    }
}
