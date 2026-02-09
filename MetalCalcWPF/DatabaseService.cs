using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService()
        {
            string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(docFolder, "MetalCalc");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _dbPath = Path.Combine(appFolder, "workshop.db");

            using (var db = new SQLiteConnection(_dbPath))
            {
                // Создаем таблицы
                db.CreateTable<WorkshopSettings>();
                db.CreateTable<MaterialProfile>();
                db.CreateTable<OrderHistory>();
                db.CreateTable<BendingProfile>();
                db.CreateTable<MaterialType>();
                db.CreateTable<WeldingProfile>(); // ✅ НОВАЯ ТАБЛИЦА

                // --- АВТО-ЗАПОЛНЕНИЕ ЛАЗЕРА ---
                if (db.Table<MaterialProfile>().Count() == 0)
                {
                    var list = new System.Collections.Generic.List<MaterialProfile>
                    {
                        // Воздух
                        new MaterialProfile { Thickness = 0.5, GasType = "Air", CuttingSpeed = 25.0, PiercePrice = 10, MarkupCoefficient = 150 },
                        new MaterialProfile { Thickness = 1.0, GasType = "Air", CuttingSpeed = 25.0, PiercePrice = 20, MarkupCoefficient = 140 },
                        new MaterialProfile { Thickness = 1.5, GasType = "Air", CuttingSpeed = 20.0, PiercePrice = 30, MarkupCoefficient = 130 },
                        new MaterialProfile { Thickness = 2.0, GasType = "Air", CuttingSpeed = 20.0, PiercePrice = 40, MarkupCoefficient = 120 },
                        new MaterialProfile { Thickness = 3.0, GasType = "Air", CuttingSpeed = 20.0, PiercePrice = 50, MarkupCoefficient = 110 },
                        new MaterialProfile { Thickness = 4.0, GasType = "Air", CuttingSpeed = 18.0, PiercePrice = 60, MarkupCoefficient = 100 },
                        new MaterialProfile { Thickness = 5.0, GasType = "Air", CuttingSpeed = 17.0, PiercePrice = 70, MarkupCoefficient = 90 },
                        new MaterialProfile { Thickness = 6.0, GasType = "Air", CuttingSpeed = 12.0,  PiercePrice = 80, MarkupCoefficient = 80 },
                        new MaterialProfile { Thickness = 8.0, GasType = "Air", CuttingSpeed = 9.4,  PiercePrice = 90, MarkupCoefficient = 70 },
                        new MaterialProfile { Thickness = 10.0, GasType = "Air", CuttingSpeed = 6.0, PiercePrice = 100, MarkupCoefficient = 60 },

                        // Кислород
                        new MaterialProfile { Thickness = 12.0, GasType = "Oxygen", CuttingSpeed = 1.8, PiercePrice = 110, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 14.0, GasType = "Oxygen", CuttingSpeed = 1.7, PiercePrice = 120, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 16.0, GasType = "Oxygen", CuttingSpeed = 1.5, PiercePrice = 130, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 18.0, GasType = "Oxygen", CuttingSpeed = 1.25, PiercePrice = 140, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 20.0, GasType = "Oxygen", CuttingSpeed = 1.1, PiercePrice = 150, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 22.0, GasType = "Oxygen", CuttingSpeed = 1.1, PiercePrice = 160, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 25.0, GasType = "Oxygen", CuttingSpeed = 0.8, PiercePrice = 170, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 30.0, GasType = "Oxygen", CuttingSpeed = 0.5, PiercePrice = 180, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 32.0, GasType = "Oxygen", CuttingSpeed = 0.2, PiercePrice = 190, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 35.0, GasType = "Oxygen", CuttingSpeed = 0.2, PiercePrice = 200, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 40.0, GasType = "Oxygen", CuttingSpeed = 0.2, PiercePrice = 210, MarkupCoefficient = 35 },
                    };
                    db.InsertAll(list);
                }

                // --- АВТО-ЗАПОЛНЕНИЕ ГИБКИ ---
                if (db.Table<BendingProfile>().Count() == 0)
                {
                    var bendList = new System.Collections.Generic.List<BendingProfile>
                    {
                        new BendingProfile { Thickness = 0.5, V_Die = 6,  MinFlange = 5,  PriceLen1500 = 100, PriceLen3000 = 250, PriceLen6000 = 600, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 1.0, V_Die = 8,  MinFlange = 6,  PriceLen1500 = 100, PriceLen3000 = 250, PriceLen6000 = 600, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 1.5, V_Die = 12, MinFlange = 8,  PriceLen1500 = 100, PriceLen3000 = 250, PriceLen6000 = 600, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 2.0, V_Die = 16, MinFlange = 11, PriceLen1500 = 120, PriceLen3000 = 300, PriceLen6000 = 700, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 3.0, V_Die = 26, MinFlange = 18, PriceLen1500 = 150, PriceLen3000 = 400, PriceLen6000 = 1000, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 4.0, V_Die = 32, MinFlange = 22, PriceLen1500 = 250, PriceLen3000 = 600, PriceLen6000 = 2000, SetupPrice = 3000 },
                        new BendingProfile { Thickness = 5.0, V_Die = 40, MinFlange = 28, PriceLen1500 = 250, PriceLen3000 = 600, PriceLen6000 = 2000, SetupPrice = 3000 },
                        new BendingProfile { Thickness = 6.0, V_Die = 50, MinFlange = 35, PriceLen1500 = 350, PriceLen3000 = 800, PriceLen6000 = 2500, SetupPrice = 3000 },
                        new BendingProfile { Thickness = 8.0, V_Die = 60,  MinFlange = 45, PriceLen1500 = 500,  PriceLen3000 = 1200, PriceLen6000 = 4000,  SetupPrice = 5000 },
                        new BendingProfile { Thickness = 10.0, V_Die = 80, MinFlange = 55, PriceLen1500 = 800,  PriceLen3000 = 2000, PriceLen6000 = 6000,  SetupPrice = 8000 },
                        new BendingProfile { Thickness = 12.0, V_Die = 100,MinFlange = 70, PriceLen1500 = 1200, PriceLen3000 = 3000, PriceLen6000 = 9000,  SetupPrice = 10000 },
                        new BendingProfile { Thickness = 14.0, V_Die = 130,MinFlange = 90, PriceLen1500 = 1500, PriceLen3000 = 4000, PriceLen6000 = 12000, SetupPrice = 12000 },
                        new BendingProfile { Thickness = 16.0, V_Die = 160,MinFlange = 110,PriceLen1500 = 2000, PriceLen3000 = 5000, PriceLen6000 = 15000, SetupPrice = 15000 },
                        new BendingProfile { Thickness = 20.0, V_Die = 250,MinFlange = 150,PriceLen1500 = 3500, PriceLen3000 = 8000, PriceLen6000 = 25000, SetupPrice = 20000 },
                    };
                    db.InsertAll(bendList);
                }

                // --- ✅ АВТО-ЗАПОЛНЕНИЕ СВАРКИ (Данные из Excel) ---
                if (db.Table<WeldingProfile>().Count() == 0)
                {
                    var weldList = new System.Collections.Generic.List<WeldingProfile>
                    {
                        // Катет 3мм
                        new WeldingProfile { FilletSize = 3.0, WeldingSpeed = 45, WeightPerCm = 0.7, CostPerCm = 4.50m, PricePerCm = 13.51m, MarkupCoefficient = 3.0 },
                        
                        // Катет 4мм
                        new WeldingProfile { FilletSize = 4.0, WeldingSpeed = 35, WeightPerCm = 1.1, CostPerCm = 6.10m, PricePerCm = 18.29m, MarkupCoefficient = 3.0 },
                        
                        // Катет 5мм
                        new WeldingProfile { FilletSize = 5.0, WeldingSpeed = 25, WeightPerCm = 1.8, CostPerCm = 8.93m, PricePerCm = 26.80m, MarkupCoefficient = 3.0 },
                        
                        // Катет 6мм
                        new WeldingProfile { FilletSize = 6.0, WeldingSpeed = 20, WeightPerCm = 2.7, CostPerCm = 11.85m, PricePerCm = 35.56m, MarkupCoefficient = 3.0 },
                        
                        // Катет 8мм
                        new WeldingProfile { FilletSize = 8.0, WeldingSpeed = 14, WeightPerCm = 4.7, CostPerCm = 18.22m, PricePerCm = 54.67m, MarkupCoefficient = 3.0 },
                        
                        // Катет 10мм
                        new WeldingProfile { FilletSize = 10.0, WeldingSpeed = 9, WeightPerCm = 7.2, CostPerCm = 28.18m, PricePerCm = 84.54m, MarkupCoefficient = 3.0 },
                        
                        // Катет 12мм
                        new WeldingProfile { FilletSize = 12.0, WeldingSpeed = 6, WeightPerCm = 10.5, CostPerCm = 41.81m, PricePerCm = 125.43m, MarkupCoefficient = 3.0 },
                        
                        // Катет 16мм
                        new WeldingProfile { FilletSize = 16.0, WeldingSpeed = 4, WeightPerCm = 19.0, CostPerCm = 67.69m, PricePerCm = 203.06m, MarkupCoefficient = 3.0 },
                        
                        // Катет 20мм
                        new WeldingProfile { FilletSize = 20.0, WeldingSpeed = 3, WeightPerCm = 30.0, CostPerCm = 107.69m, PricePerCm = 323.06m, MarkupCoefficient = 3.0 },
                    };
                    db.InsertAll(weldList);
                }

                // --- АВТО-ЗАПОЛНЕНИЕ МАТЕРИАЛОВ ---
                if (db.Table<MaterialType>().Count() == 0)
                {
                    var materials = new System.Collections.Generic.List<MaterialType>
                    {
                        new MaterialType { Name = "Черная сталь (Ст3)", Density = 7.85, BasePricePerKg = 355m },
                        new MaterialType { Name = "Оцинковка", Density = 7.85, BasePricePerKg = 450m },
                        new MaterialType { Name = "Нержавейка (AISI 304)", Density = 7.9, BasePricePerKg = 2500m }
                    };
                    db.InsertAll(materials);
                }
            }
        }

        public WorkshopSettings GetSettings()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                var settings = db.Table<WorkshopSettings>().FirstOrDefault();
                if (settings == null)
                {
                    settings = new WorkshopSettings();
                    db.Insert(settings);
                }
                return settings;
            }
        }

        public void SaveSettings(WorkshopSettings settings)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Update(settings);
            }
        }

        public MaterialProfile GetProfileByThickness(double thickness)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<MaterialProfile>()
                         .Where(p => p.Thickness >= thickness)
                         .OrderBy(p => p.Thickness)
                         .FirstOrDefault();
            }
        }

        public BendingProfile GetBendingProfile(double thickness)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<BendingProfile>()
                         .Where(p => p.Thickness >= thickness)
                         .OrderBy(p => p.Thickness)
                         .FirstOrDefault();
            }
        }

        // ✅ НОВЫЙ МЕТОД: Найти профиль сварки по толщине металла
        public WeldingProfile GetWeldingProfile(double thickness)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                // Катет шва обычно = 0.7 × Толщина металла
                double estimatedFillet = thickness * 0.7;
                
                return db.Table<WeldingProfile>()
                         .Where(p => p.FilletSize >= estimatedFillet)
                         .OrderBy(p => p.FilletSize)
                         .FirstOrDefault();
            }
        }

        public void SaveOrder(OrderHistory order)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Insert(order);
            }
        }

        public void DeleteOrder(int id)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Delete<OrderHistory>(id);
            }
        }

        public List<OrderHistory> GetRecentOrders()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<OrderHistory>()
                         .OrderByDescending(o => o.CreatedDate)
                         .Take(50)
                         .ToList();
            }
        }

        public List<MaterialType> GetMaterials()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<MaterialType>().ToList();
            }
        }

        public void UpdateAllMaterials(List<MaterialType> list)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.DeleteAll<MaterialType>();
                db.InsertAll(list);
            }
        }

        public List<MaterialProfile> GetAllLaserProfiles()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<MaterialProfile>().OrderBy(p => p.Thickness).ToList();
            }
        }

        public void UpdateAllLaserProfiles(List<MaterialProfile> list)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.DeleteAll<MaterialProfile>();
                db.InsertAll(list);
            }
        }

        public List<BendingProfile> GetAllBendingProfiles()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<BendingProfile>().OrderBy(p => p.Thickness).ToList();
            }
        }

        public void UpdateAllBendingProfiles(List<BendingProfile> list)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.DeleteAll<BendingProfile>();
                db.InsertAll(list);
            }
        }

        // ✅ НОВЫЕ МЕТОДЫ для СВАРКИ
        public List<WeldingProfile> GetAllWeldingProfiles()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<WeldingProfile>().OrderBy(p => p.FilletSize).ToList();
            }
        }

        public void UpdateAllWeldingProfiles(List<WeldingProfile> list)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.DeleteAll<WeldingProfile>();
                db.InsertAll(list);
            }
        }
    }
}
