using SQLite;
using System.IO;
using MetalCalcWPF.Models; // Подключаем наши модели
using System.Linq; // Нужно для FirstOrDefault

namespace MetalCalcWPF
{
    public class DatabaseService
    {
        // Путь: C:\Users\User\Documents\MetalCalc\workshop.db
        private readonly string _dbPath;

        public DatabaseService()
        {
            // 1. Получаем путь к "Мои Документы"
            string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(docFolder, "MetalCalc");

            // 2. Если папки нет - создаем
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            // 3. Полный путь к файлу
            _dbPath = Path.Combine(appFolder, "workshop.db");

            using (var db = new SQLiteConnection(_dbPath))
            {
                // Создаем таблицы
                db.CreateTable<WorkshopSettings>();
                db.CreateTable<MaterialProfile>();
                db.CreateTable<OrderHistory>();
                db.CreateTable<BendingProfile>();
                db.CreateTable<MaterialType>();

                // --- АВТО-ЗАПОЛНЕНИЕ ЛАЗЕРА ---
                if (db.Table<MaterialProfile>().Count() == 0)
                {
                    var list = new System.Collections.Generic.List<MaterialProfile>
                    {
                        // Воздух
                        new MaterialProfile { Thickness = 0.5, GasType = "Air", CuttingSpeed = 30.0, PiercePrice = 10, MarkupCoefficient = 150 },
                        new MaterialProfile { Thickness = 1.0, GasType = "Air", CuttingSpeed = 25.0, PiercePrice = 20, MarkupCoefficient = 140 },
                        new MaterialProfile { Thickness = 1.5, GasType = "Air", CuttingSpeed = 22.0, PiercePrice = 30, MarkupCoefficient = 130 },
                        new MaterialProfile { Thickness = 2.0, GasType = "Air", CuttingSpeed = 20.0, PiercePrice = 40, MarkupCoefficient = 120 },
                        new MaterialProfile { Thickness = 3.0, GasType = "Air", CuttingSpeed = 15.0, PiercePrice = 50, MarkupCoefficient = 110 },
                        new MaterialProfile { Thickness = 4.0, GasType = "Air", CuttingSpeed = 13.0, PiercePrice = 60, MarkupCoefficient = 100 },
                        new MaterialProfile { Thickness = 5.0, GasType = "Air", CuttingSpeed = 11.2, PiercePrice = 70, MarkupCoefficient = 90 },
                        new MaterialProfile { Thickness = 6.0, GasType = "Air", CuttingSpeed = 8.0,  PiercePrice = 80, MarkupCoefficient = 80 },
                        new MaterialProfile { Thickness = 8.0, GasType = "Air", CuttingSpeed = 5.5,  PiercePrice = 90, MarkupCoefficient = 70 },
                        new MaterialProfile { Thickness = 10.0, GasType = "Air", CuttingSpeed = 3.5, PiercePrice = 100, MarkupCoefficient = 60 },

                        // Кислород
                        new MaterialProfile { Thickness = 12.0, GasType = "Oxygen", CuttingSpeed = 1.8, PiercePrice = 110, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 14.0, GasType = "Oxygen", CuttingSpeed = 1.6, PiercePrice = 120, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 16.0, GasType = "Oxygen", CuttingSpeed = 1.4, PiercePrice = 130, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 18.0, GasType = "Oxygen", CuttingSpeed = 1.2, PiercePrice = 140, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 20.0, GasType = "Oxygen", CuttingSpeed = 1.0, PiercePrice = 150, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 22.0, GasType = "Oxygen", CuttingSpeed = 0.9, PiercePrice = 160, MarkupCoefficient = 40 },
                        new MaterialProfile { Thickness = 25.0, GasType = "Oxygen", CuttingSpeed = 0.7, PiercePrice = 170, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 30.0, GasType = "Oxygen", CuttingSpeed = 0.5, PiercePrice = 180, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 32.0, GasType = "Oxygen", CuttingSpeed = 0.4, PiercePrice = 190, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 36.0, GasType = "Oxygen", CuttingSpeed = 0.3, PiercePrice = 200, MarkupCoefficient = 35 },
                        new MaterialProfile { Thickness = 40.0, GasType = "Oxygen", CuttingSpeed = 0.2, PiercePrice = 210, MarkupCoefficient = 35 },
                    };
                    db.InsertAll(list);
                }

                // --- АВТО-ЗАПОЛНЕНИЕ ГИБКИ (Данные из твоего Excel) ---
                if (db.Table<BendingProfile>().Count() == 0)
                {
                    var bendList = new System.Collections.Generic.List<BendingProfile>
                    {
                        // Тонкие (Наладка 2000)
                        new BendingProfile { Thickness = 0.5, V_Die = 6,  MinFlange = 5,  PriceLen1500 = 100, PriceLen3000 = 250, PriceLen6000 = 600, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 1.0, V_Die = 8,  MinFlange = 6,  PriceLen1500 = 100, PriceLen3000 = 250, PriceLen6000 = 600, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 1.5, V_Die = 12, MinFlange = 8,  PriceLen1500 = 100, PriceLen3000 = 250, PriceLen6000 = 600, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 2.0, V_Die = 16, MinFlange = 11, PriceLen1500 = 120, PriceLen3000 = 300, PriceLen6000 = 700, SetupPrice = 2000 },
                        
                        // Средние (Наладка растет)
                        new BendingProfile { Thickness = 3.0, V_Die = 26, MinFlange = 18, PriceLen1500 = 150, PriceLen3000 = 400, PriceLen6000 = 1000, SetupPrice = 2000 },
                        new BendingProfile { Thickness = 4.0, V_Die = 32, MinFlange = 22, PriceLen1500 = 250, PriceLen3000 = 600, PriceLen6000 = 2000, SetupPrice = 3000 },
                        new BendingProfile { Thickness = 5.0, V_Die = 40, MinFlange = 28, PriceLen1500 = 250, PriceLen3000 = 600, PriceLen6000 = 2000, SetupPrice = 3000 },
                        new BendingProfile { Thickness = 6.0, V_Die = 50, MinFlange = 35, PriceLen1500 = 350, PriceLen3000 = 800, PriceLen6000 = 2500, SetupPrice = 3000 },

                        // Толстые (Высокая цена и дорогая наладка)
                        new BendingProfile { Thickness = 8.0, V_Die = 60,  MinFlange = 45, PriceLen1500 = 500,  PriceLen3000 = 1200, PriceLen6000 = 4000,  SetupPrice = 5000 },
                        new BendingProfile { Thickness = 10.0, V_Die = 80, MinFlange = 55, PriceLen1500 = 800,  PriceLen3000 = 2000, PriceLen6000 = 6000,  SetupPrice = 8000 },
                        new BendingProfile { Thickness = 12.0, V_Die = 100,MinFlange = 70, PriceLen1500 = 1200, PriceLen3000 = 3000, PriceLen6000 = 9000,  SetupPrice = 10000 },
                        new BendingProfile { Thickness = 14.0, V_Die = 130,MinFlange = 90, PriceLen1500 = 1500, PriceLen3000 = 4000, PriceLen6000 = 12000, SetupPrice = 12000 },
                        new BendingProfile { Thickness = 16.0, V_Die = 160,MinFlange = 110,PriceLen1500 = 2000, PriceLen3000 = 5000, PriceLen6000 = 15000, SetupPrice = 15000 },
                        
                        // Супер-тяжелые
                        new BendingProfile { Thickness = 20.0, V_Die = 250,MinFlange = 150,PriceLen1500 = 3500, PriceLen3000 = 8000, PriceLen6000 = 25000, SetupPrice = 20000 },
                    };
                    db.InsertAll(bendList);
                }

                // --- АВТО-ЗАПОЛНЕНИЕ МАТЕРИАЛОВ ---
                if (db.Table<MaterialType>().Count() == 0)
                {
                    var materials = new System.Collections.Generic.List<MaterialType>
                    {
                        // Плотность стали ~7.85 г/см3
                        new MaterialType { Name = "Черная сталь (Ст3)", Density = 7.85, BasePricePerKg = 355 },
                        new MaterialType { Name = "Оцинковка", Density = 7.85, BasePricePerKg = 450 },     // Примерная цена
                        new MaterialType { Name = "Нержавейка (AISI 304)", Density = 7.9, BasePricePerKg = 2500 } // Примерная цена
                    };
                    db.InsertAll(materials);
                }
            }
        }

        // Получить настройки
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

        // Сохранить настройки
        public void SaveSettings(WorkshopSettings settings)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Update(settings);
            }
        }

        // Найти профиль Лазера
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

        // Найти профиль Гибки
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

        // Сохранить заказ
        public void SaveOrder(OrderHistory order)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Insert(order);
            }
        }

        // !!! НОВОЕ: Удалить заказ по ID
        public void DeleteOrder(int id)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Delete<OrderHistory>(id);
            }
        }

        // Получить последние 50 заказов
        public System.Collections.Generic.List<OrderHistory> GetRecentOrders()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<OrderHistory>()
                         .OrderByDescending(o => o.CreatedDate)
                         .Take(50)
                         .ToList();
            }
        }

        public System.Collections.Generic.List<MaterialType> GetMaterials()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<MaterialType>().ToList();
            }
        }
    }
}