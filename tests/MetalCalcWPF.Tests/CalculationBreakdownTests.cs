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
            public List<OrderHistory> GetOrdersByDateRange(DateTime startInclusive, DateTime endExclusive) => new List<OrderHistory>();
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
            public List<CuttingMachine> GetCuttingMachinesByKind(CuttingMachineKind kind)
                => CuttingMachines.Where(m => m.Kind == kind).ToList();
            public CuttingMachine? GetCuttingMachineById(int id)
                => CuttingMachines.FirstOrDefault(m => m.Id == id);
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
        /// Для партии &gt; 1 шт должна появиться строка «Итого за N шт», умноженная на количество,
        /// а итоговая «Итого по лазеру» пометиться как IsTotal (со Спринта 2.2b — единая строка
        /// итога, куда накидываются Setup и MinCharge от станка).
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

            var total = laser.Lines.First(l => l.IsTotal);
            Assert.AreEqual("Итого по лазеру", total.Label, "Итоговая строка должна называться «Итого по лазеру»");
            Assert.AreEqual(1000m, total.Value);
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
        /// Спринт 2.2b: если у выбранного станка задан PricePerMeterOverride,
        /// он подменяет «цену клиенту за метр», а формула минута/скорость × K
        /// в детализации показывается отдельной строкой «Цена по формуле за метр».
        /// </summary>
        [TestMethod]
        public void Laser_Machine_PricePerMeterOverride_ReplacesClientPrice()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 10, GasType = "Air",
                CuttingSpeed = 10.0, PiercePrice = 0, MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;  // по формуле клиент/м = 50/10*40 = 200
            db.Settings.MaterialMarkupPercent = 0;
            db.CuttingMachines.Add(new CuttingMachine
            {
                Id = 1, Name = "Лазер договорной", Kind = CuttingMachineKind.Laser,
                IsActive = true,
                PricePerMeterOverride = 500m,  // клиент всегда платит 500 тг/м независимо от формулы
            });

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 10, 1, mat, 1.0, 0, false, 0, 0, false, 0,
                                       cuttingMachineId: 1);

            // 500 тг/м × 1 м = 500 тг, не 200 тг по формуле.
            Assert.AreEqual(500m, Math.Round(r.LaserCost, 2));

            var laser = r.Breakdowns.First(b => b.Section.Contains("Лазер"));
            Assert.IsTrue(laser.Lines.Any(l => l.Label == "Цена по формуле за метр"),
                "Формульная цена должна остаться видна для сверки");
            Assert.IsTrue(laser.Lines.Any(l => l.Label == "Цена по станку (override)"),
                "Override должен быть подписан отдельной строкой");
        }

        /// <summary>
        /// SetupCostPerJob у станка прибавляется к итогу ОДИН раз за партию,
        /// не умножается на количество.
        /// </summary>
        [TestMethod]
        public void Laser_Machine_SetupCost_AddsOnceNotPerPart()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 10, GasType = "Air",
                CuttingSpeed = 10.0, PiercePrice = 0, MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;  // клиент/м = 200 тг
            db.Settings.MaterialMarkupPercent = 0;
            db.CuttingMachines.Add(new CuttingMachine
            {
                Id = 1, Name = "Лазер", Kind = CuttingMachineKind.Laser,
                IsActive = true,
                SetupCostPerJob = 1500m,
            });

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            // 5 шт × 1 м × 200 тг/м = 1000 тг резки + 1500 тг setup (разово) = 2500 тг
            var r = svc.CalculateOrder(100, 100, 10, 5, mat, 1.0, 0, false, 0, 0, false, 0,
                                       cuttingMachineId: 1);

            Assert.AreEqual(2500m, Math.Round(r.LaserCost, 2),
                "Setup прибавляется один раз ко всей партии, не 5 раз");
        }

        /// <summary>
        /// MinChargePerJob работает как пол: если subtotal меньше, итог
        /// подтягивается вверх до минимума.
        /// </summary>
        [TestMethod]
        public void Laser_Machine_MinCharge_ActsAsFloor()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 1, GasType = "Air",
                CuttingSpeed = 25.0, PiercePrice = 0, MarkupCoefficient = 100,
            });
            db.Settings.LaserAirMinutePrice = 50m;  // клиент/м = 50/25*100 = 200 тг
            db.Settings.MaterialMarkupPercent = 0;
            db.CuttingMachines.Add(new CuttingMachine
            {
                Id = 1, Name = "Лазер", Kind = CuttingMachineKind.Laser,
                IsActive = true,
                MinChargePerJob = 5000m,
            });

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            // 1 м × 200 тг/м = 200 тг, пол 5000 → итог 5000
            var r = svc.CalculateOrder(100, 100, 1, 1, mat, 1.0, 0, false, 0, 0, false, 0,
                                       cuttingMachineId: 1);

            Assert.AreEqual(5000m, Math.Round(r.LaserCost, 2));

            var laser = r.Breakdowns.First(b => b.Section.Contains("Лазер"));
            Assert.IsTrue(laser.Lines.Any(l => l.Label == "Минимум за заказ"),
                "Строка «Минимум за заказ» должна появиться, когда пол сработал");
        }

        /// <summary>
        /// Когда заказ УЖЕ больше MinCharge, пол не применяется — платим ровно по расчёту.
        /// </summary>
        [TestMethod]
        public void Laser_Machine_MinCharge_NotAppliedWhenSubtotalAbove()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 1, GasType = "Air",
                CuttingSpeed = 25.0, PiercePrice = 0, MarkupCoefficient = 100,
            });
            db.Settings.LaserAirMinutePrice = 50m;  // клиент/м = 200 тг
            db.Settings.MaterialMarkupPercent = 0;
            db.CuttingMachines.Add(new CuttingMachine
            {
                Id = 1, Name = "Лазер", Kind = CuttingMachineKind.Laser,
                IsActive = true,
                MinChargePerJob = 500m,
            });

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            // 10 шт × 1 м × 200 тг/м = 2000 тг. Пол 500 → остаётся 2000.
            var r = svc.CalculateOrder(100, 100, 1, 10, mat, 1.0, 0, false, 0, 0, false, 0,
                                       cuttingMachineId: 1);

            Assert.AreEqual(2000m, Math.Round(r.LaserCost, 2));
            var laser = r.Breakdowns.First(b => b.Section.Contains("Лазер"));
            Assert.IsFalse(laser.Lines.Any(l => l.Label == "Минимум за заказ"),
                "Когда MinCharge не применён, эту строку показывать не должны");
        }

        /// <summary>
        /// Если станков в справочнике нет — калькулятор работает по чистой Excel-формуле
        /// (регрессия против сценария до 2.2b и для тех, кто ещё не заполнил таблицу станков).
        /// </summary>
        [TestMethod]
        public void Laser_NoMachinesInDb_WorksWithPureExcelFormula()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 16, GasType = "Oxygen",
                CuttingSpeed = 1.4, PiercePrice = 0, MarkupCoefficient = 40,
            });
            db.Settings.LaserOxygenMinutePrice = 85m;
            db.Settings.MaterialMarkupPercent = 0;
            // CuttingMachines пуст — это легаси-сценарий.

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 16, 1, mat, 1.0, 0, false, 0, 0, false, 0);

            Assert.AreEqual(2428.57m, Math.Round(r.LaserCost, 2),
                "Без станка должен работать чистый Excel-паритет");
        }

        /// <summary>
        /// Неактивные станки игнорируются — если явно переданный ID указывает на IsActive=false,
        /// калькулятор откатится на первый активный.
        /// </summary>
        [TestMethod]
        public void Laser_InactiveMachineIsSkipped()
        {
            var db = new FakeDb();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 10, GasType = "Air",
                CuttingSpeed = 10.0, PiercePrice = 0, MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;
            db.Settings.MaterialMarkupPercent = 0;
            db.CuttingMachines.Add(new CuttingMachine
            {
                Id = 1, Name = "Старый", Kind = CuttingMachineKind.Laser,
                IsActive = false,
                PricePerMeterOverride = 999m,  // не должен подмешаться
            });
            db.CuttingMachines.Add(new CuttingMachine
            {
                Id = 2, Name = "Основной", Kind = CuttingMachineKind.Laser,
                IsActive = true,
                SetupCostPerJob = 100m,
            });

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            // Указываем ID неактивного — должен упасть на первый активный = id=2
            var r = svc.CalculateOrder(100, 100, 10, 1, mat, 1.0, 0, false, 0, 0, false, 0,
                                       cuttingMachineId: 1);

            // 1 шт × 200 тг + 100 setup от активного = 300, а не 999
            Assert.AreEqual(300m, Math.Round(r.LaserCost, 2));
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
