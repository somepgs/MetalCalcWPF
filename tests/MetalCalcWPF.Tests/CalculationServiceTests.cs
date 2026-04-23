using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetalCalcWPF.Services;
using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.Models;
using System.Collections.Generic;

namespace MetalCalcWPF.Tests
{
    [TestClass]
    public class CalculationServiceTests
    {
        private class FakeDb : IDatabaseService
        {
            public WorkshopSettings Settings { get; set; } = new WorkshopSettings();
            public List<MaterialProfile> Profiles { get; set; } = new List<MaterialProfile>();

            public FakeDb()
            {
                Profiles.Add(new MaterialProfile { Thickness = 1.0, GasType = "Air", CuttingSpeed = 25.0, PiercePrice = 10, MarkupCoefficient = 100 });
                Profiles.Add(new MaterialProfile { Thickness = 12.0, GasType = "Oxygen", CuttingSpeed = 1.8, PiercePrice = 120, MarkupCoefficient = 40 });
            }

            public WorkshopSettings GetSettings() => Settings;
            public void SaveSettings(WorkshopSettings settings) { Settings = settings; }
            public MaterialProfile? GetProfileByThickness(double thickness)
            {
                foreach (var p in Profiles)
                {
                    if (p.Thickness >= thickness) return p;
                }
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
            public List<RolledProfile> GetRolledProfilesByKind(ProfileKind kind)
            {
                var result = new List<RolledProfile>();
                foreach (var p in RolledProfiles)
                {
                    if (p.Kind == kind) result.Add(p);
                }
                return result;
            }
            public RolledProfile? GetRolledProfileById(int id)
            {
                foreach (var p in RolledProfiles)
                {
                    if (p.Id == id) return p;
                }
                return null;
            }
            public int AddRolledProfile(RolledProfile profile)
            {
                profile.Id = RolledProfiles.Count + 1;
                RolledProfiles.Add(profile);
                return profile.Id;
            }
            public void UpdateRolledProfile(RolledProfile profile) { }
            public void DeleteRolledProfile(int id)
            {
                RolledProfiles.RemoveAll(p => p.Id == id);
            }
            public void UpdateAllRolledProfiles(List<RolledProfile> list) { RolledProfiles = list; }

            public List<CuttingMachine> CuttingMachines { get; set; } = new List<CuttingMachine>();
            public List<CuttingMachine> GetAllCuttingMachines() => CuttingMachines;
            public List<CuttingMachine> GetCuttingMachinesByKind(CuttingMachineKind kind)
            {
                var result = new List<CuttingMachine>();
                foreach (var m in CuttingMachines) if (m.Kind == kind) result.Add(m);
                return result;
            }
            public CuttingMachine? GetCuttingMachineById(int id)
            {
                foreach (var m in CuttingMachines) if (m.Id == id) return m;
                return null;
            }
            public int AddCuttingMachine(CuttingMachine machine)
            {
                machine.Id = CuttingMachines.Count + 1;
                CuttingMachines.Add(machine);
                return machine.Id;
            }
            public void UpdateCuttingMachine(CuttingMachine machine) { }
            public void DeleteCuttingMachine(int id) { CuttingMachines.RemoveAll(m => m.Id == id); }
            public void UpdateAllCuttingMachines(List<CuttingMachine> list) { CuttingMachines = list; }
        }

        [TestMethod]
        public void OxygenMakesCuttingMoreExpensiveThanAir()
        {
            var db = new FakeDb();
            var svc = new CalculationService(db);

            // set realistic settings
            db.Settings.OperatorMonthlySalary = 300000;
            db.Settings.ElectricityPricePerKw = 25;
            db.Settings.AmortizationPerHour = 650;
            db.Settings.OxygenBottlePrice = 5000;
            db.Settings.OxygenBottleVolumeLiters = 40;
            db.Settings.OxygenBottlePressureAtm = 150;
            db.Settings.OxygenFlowRateLpm = 15;

            // 1m of cut at thickness 1 (air)
            var rAir = svc.CalculateOrder(100, 100, 1, 1, new MaterialType { Name = "St", Density = 7.85, BasePricePerKg = 1000 }, 1.0, 0, false, 0, 0, false, 0, 0);
            // 1m of cut at thickness 12 (oxygen)
            var rOxy = svc.CalculateOrder(100, 100, 12, 1, new MaterialType { Name = "St", Density = 7.85, BasePricePerKg = 1000 }, 1.0, 0, false, 0, 0, false, 0, 0);

            Assert.IsTrue(rOxy.LaserCost >= rAir.LaserCost, "Ожидаем, что рез с кислородом не дешевле резки воздухом");
        }

        [TestMethod]
        public void PiercesIncreaseCost()
        {
            var db = new FakeDb();
            var svc = new CalculationService(db);
            var r0 = svc.CalculateOrder(100,100,1,1,new MaterialType { Name = "St", Density=7.85, BasePricePerKg=1000}, 1.0, 0, false,0,0,false,0,0);
            var r5 = svc.CalculateOrder(100,100,1,1,new MaterialType { Name = "St", Density=7.85, BasePricePerKg=1000}, 1.0, 5, false,0,0,false,0,0);
            Assert.IsTrue(r5.LaserCost > r0.LaserCost);
        }

        /// <summary>
        /// Excel-паритет (Спринт 2.2c): для 16мм × 1м с коэффициентом 40 и скоростью 1.4 м/мин
        /// цена кислородной минуты 85 тг/мин должна давать ровно 2428.57 тг
        /// — как в рабочем Excel-исходнике («Лазер bodor», строка 14).
        /// Регрессия против бага, когда коэффициент трактовался как процент (×1.4 вместо ×40).
        /// </summary>
        [TestMethod]
        public void Laser_16mm_1m_MatchesExcel_2428_57()
        {
            var db = new FakeDb();
            db.Profiles.Clear();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 16,
                GasType = "Oxygen",
                CuttingSpeed = 1.4,
                PiercePrice = 130,
                MarkupCoefficient = 40,
            });
            db.Settings.LaserOxygenMinutePrice = 85m;
            db.Settings.MaterialMarkupPercent = 0;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 16, 1, mat,
                laserLengthMeters: 1.0, piercesCount: 0,
                useBending: false, bendsCount: 0, bendLengthMm: 0,
                useWelding: false, weldLengthCm: 0);

            Assert.AreEqual(2428.57m, Math.Round(r.LaserCost, 2),
                "Формула Excel: (85 / 1.4) × 40 = 2428.571 тг");
        }

        /// <summary>
        /// Коэффициент надбавки — прямой множитель: при K=40 цена реза должна быть
        /// ровно в 40 раз больше себестоимости, а не в 1.4 раза (старый баг с процентом).
        /// </summary>
        [TestMethod]
        public void Laser_MarkupCoefficient_IsDirectMultiplier_NotPercent()
        {
            var db = new FakeDb();
            db.Profiles.Clear();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 10,
                GasType = "Air",
                CuttingSpeed = 10.0,
                PiercePrice = 0,
                MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;
            db.Settings.MaterialMarkupPercent = 0;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r = svc.CalculateOrder(100, 100, 10, 1, mat,
                laserLengthMeters: 1.0, piercesCount: 0,
                useBending: false, bendsCount: 0, bendLengthMm: 0,
                useWelding: false, weldLengthCm: 0);

            // Себест = 50 / 10 = 5 тг/м. Клиент = 5 × 40 = 200 тг/м. За 1м → 200 тг.
            Assert.AreEqual(200m, Math.Round(r.LaserCost, 2));
        }

        /// <summary>
        /// Пробивки в новой модели складываются «как есть» и НЕ умножаются на коэффициент:
        /// в Excel столбец E («Цена за 1 пробивку») — уже клиентская цена.
        /// </summary>
        [TestMethod]
        public void Laser_PiercesAddedFlat_NotMarkedUp()
        {
            var db = new FakeDb();
            db.Profiles.Clear();
            db.Profiles.Add(new MaterialProfile
            {
                Thickness = 5,
                GasType = "Air",
                CuttingSpeed = 10.0,
                PiercePrice = 100,
                MarkupCoefficient = 40,
            });
            db.Settings.LaserAirMinutePrice = 50m;
            db.Settings.MaterialMarkupPercent = 0;

            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 0 };

            var r0 = svc.CalculateOrder(100,100,5,1,mat, 1.0, 0, false,0,0,false,0,0);
            var r3 = svc.CalculateOrder(100,100,5,1,mat, 1.0, 3, false,0,0,false,0,0);

            Assert.AreEqual(200m, Math.Round(r0.LaserCost, 2));
            Assert.AreEqual(500m, Math.Round(r3.LaserCost, 2),
                "Каждая пробивка добавляет ровно PiercePrice (100 тг), без наценки");
        }

        [TestMethod]
        public void RolledProfile_MassScalesLinearlyWithLength()
        {
            // Имитируем режим сортамента: масса = длина × кг/м × кол-во.
            // Уголок 50×50×5 по ГОСТ: 3.77 кг/м.
            var db = new FakeDb();
            db.Settings.MaterialMarkupPercent = 0; // чистая закупочная цена, чтобы легко считать
            var svc = new CalculationService(db);

            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 500 };

            // 1 шт × 1 м × 3.77 кг/м = 3.77 кг → material ≈ 3.77 × 500 = 1885 тг
            var r1m = svc.CalculateOrder(0, 0, 5, 1, mat, 0, 0, false, 0, 0, false, 0, measuredWeightKg: 1 * 3.77);
            // 1 шт × 3 м × 3.77 кг/м = 11.31 кг → material ≈ 11.31 × 500 = 5655 тг
            var r3m = svc.CalculateOrder(0, 0, 5, 1, mat, 0, 0, false, 0, 0, false, 0, measuredWeightKg: 3 * 3.77);

            Assert.IsTrue(r1m.MaterialCost > 0, "Стоимость металла по массе должна быть > 0");
            Assert.AreEqual(3.0, (double)(r3m.MaterialCost / r1m.MaterialCost), 0.01,
                "При втрое большей длине стоимость металла должна быть втрое больше");
        }

        [TestMethod]
        public void RolledProfile_QuantityMultipliesMass()
        {
            // 10 швеллеров №10 (8.59 кг/м) по 6 м каждый = 515.4 кг
            var db = new FakeDb();
            db.Settings.MaterialMarkupPercent = 0;
            var svc = new CalculationService(db);
            var mat = new MaterialType { Name = "Ст3", Density = 7.85, BasePricePerKg = 400 };

            double massTotal = 10 * 6 * 8.59; // = 515.4
            var r = svc.CalculateOrder(0, 0, 5, 10, mat, 0, 0, false, 0, 0, false, 0,
                                       measuredWeightKg: massTotal);

            decimal expected = (decimal)massTotal * 400m;
            Assert.AreEqual((double)expected, (double)r.MaterialCost, 1.0,
                "Стоимость = полная масса × базовая цена");
        }
    }
}
