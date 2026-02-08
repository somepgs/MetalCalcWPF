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
            public void SaveOrder(OrderHistory order) { }
            public void DeleteOrder(int id) { }
            public List<OrderHistory> GetRecentOrders() => new List<OrderHistory>();
            public List<MaterialType> GetMaterials() => new List<MaterialType>();
            public void UpdateAllMaterials(List<MaterialType> list) { }
            public List<MaterialProfile> GetAllLaserProfiles() => Profiles;
            public void UpdateAllLaserProfiles(List<MaterialProfile> list) { Profiles = list; }
            public List<BendingProfile> GetAllBendingProfiles() => new List<BendingProfile>();
            public void UpdateAllBendingProfiles(List<BendingProfile> list) { }
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

            Assert.IsTrue(rOxy.LaserCost >= rAir.LaserCost, "ќжидаем, что резка с кислородом не дешевле воздуха");
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

        [TestMethod]
        public void MinChargeApplies()
        {
            var db = new FakeDb();
            db.Settings.LaserMinChargePerJob = 10000;
            var svc = new CalculationService(db);
            var r = svc.CalculateOrder(100,100,1,1,new MaterialType { Name = "St", Density=7.85, BasePricePerKg=1000}, 0.01, 0, false,0,0,false,0,0);
            Assert.IsTrue(r.LaserCost >= db.Settings.LaserMinChargePerJob);
        }
    }
}
