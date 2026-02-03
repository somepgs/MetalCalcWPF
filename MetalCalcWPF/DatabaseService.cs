using SQLite;
using System.IO;
using MetalCalcWPF.Models; // Подключаем наши модели

namespace MetalCalcWPF
{
    public class DatabaseService
    {
        // Путь к файлу базы данных. Он будет лежать рядом с .exe файлом
        private readonly string _dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "workshop.db");

        public DatabaseService()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                // Создаем таблицы
                db.CreateTable<WorkshopSettings>();
                db.CreateTable<MaterialProfile>();
                db.CreateTable<OrderHistory>();

                // --- АВТО-ЗАПОЛНЕНИЕ ---
                // Если таблица профилей пустая, заполняем её данными из твоего Excel
                if (db.Table<MaterialProfile>().Count() == 0)
                {
                    var list = new List<MaterialProfile>
                    {
                        // Воздух (Air)
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

                        // Кислород (Oxygen) - смена технологии!
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
            }
        }

        // Метод: Получить настройки (читаем первую строку из таблицы)
        public WorkshopSettings GetSettings()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                // Пытаемся найти первую запись. Если таблицы пустые - создаем новую "нулевую" запись.
                var settings = db.Table<WorkshopSettings>().FirstOrDefault();
                if (settings == null)
                {
                    settings = new WorkshopSettings();
                    db.Insert(settings);
                }
                return settings;
            }
        }

        // Метод: Сохранить настройки
        public void SaveSettings(WorkshopSettings settings)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Update(settings);
            }
        }

        // Метод: Найти профиль по толщине
        public MaterialProfile GetProfileByThickness(double thickness)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                // Ищем ближайшую толщину. Например, если ввели 1.2 мм, найдем 1.5 мм (или 1.0, как настроим)
                // Пока сделаем простой поиск: ищем первую запись, где толщина >= введенной
                return db.Table<MaterialProfile>()
                         .Where(p => p.Thickness >= thickness)
                         .OrderBy(p => p.Thickness)
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

        public System.Collections.Generic.List<OrderHistory> GetRecentOrders()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                // Берем таблицу, сортируем по Дате (сначала новые), берем 50 штук
                return db.Table<OrderHistory>()
                         .OrderByDescending(o => o.CreatedDate)
                         .Take(50)
                         .ToList();
            }
        }

    }
}