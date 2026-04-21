using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetalCalcWPF.Infrastructure.Migrations;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService() : this(BuildDefaultDbPath())
        {
        }

        /// <summary>
        /// Конструктор с явным путём — используется тестами.
        /// </summary>
        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath;

            // 1) Гарантируем, что папка для БД существует (если это файл, а не :memory:).
            if (!string.Equals(dbPath, ":memory:", System.StringComparison.Ordinal))
            {
                var folder = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }

            // 2) Прокатываем миграции, затем заполняем справочники (если пусто).
            using (var db = new SQLiteConnection(_dbPath))
            {
                MigrationRunner.Run(db);
                SeedIfEmpty(db);
            }
        }

        private static string BuildDefaultDbPath()
        {
            string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(docFolder, "MetalCalc");
            return Path.Combine(appFolder, "workshop.db");
        }

        /// <summary>
        /// Заполняет справочные таблицы стартовыми данными, если они пусты.
        /// Безопасно вызывать многократно — на втором запуске не делает ничего.
        /// </summary>
        private static void SeedIfEmpty(SQLiteConnection db)
        {
            {
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

                // --- ✅ АВТО-ЗАПОЛНЕНИЕ СОРТАМЕНТА ПРОКАТА ---
                if (db.Table<RolledProfile>().Count() == 0)
                {
                    db.InsertAll(BuildRolledProfileSeed());
                }
            }
        }

        /// <summary>
        /// Стартовый сид сортамента: уголки, швеллеры, двутавры, профтрубы.
        /// Значения кг/м взяты из ГОСТ-таблиц (округлены до 2-х знаков).
        /// Полный сортамент можно дополнять через редактор базы данных в приложении.
        /// </summary>
        private static List<RolledProfile> BuildRolledProfileSeed()
        {
            // Совместимость со станками для удобочитаемости
            const int MA = (int)(CuttingMachines.BandSaw | CuttingMachines.PressShears | CuttingMachines.AngleGrinder); // уголок
            const int MB = (int)(CuttingMachines.BandSaw | CuttingMachines.AngleGrinder);                                // швеллер/двутавр/труба

            var list = new List<RolledProfile>();

            // ---------- Уголок равнополочный (ГОСТ 8509-93) ----------
            void AddAngleEqual(double side, double t, double kgm)
            {
                list.Add(new RolledProfile
                {
                    Kind = ProfileKind.AngleEqual,
                    SizeCode = $"{side:0.##}x{side:0.##}x{t:0.##}",
                    GostDesignation = $"Уголок {side:0.##}×{side:0.##}×{t:0.##} ГОСТ 8509-93",
                    WeightPerMeterKg = kgm,
                    Height = side,
                    Width = side,
                    WallThickness = t,
                    CompatibleMachines = MA,
                });
            }
            AddAngleEqual(20, 3, 0.89);   AddAngleEqual(20, 4, 1.15);
            AddAngleEqual(25, 3, 1.12);   AddAngleEqual(25, 4, 1.46);   AddAngleEqual(25, 5, 1.78);
            AddAngleEqual(32, 3, 1.46);   AddAngleEqual(32, 4, 1.91);
            AddAngleEqual(40, 3, 1.85);   AddAngleEqual(40, 4, 2.42);   AddAngleEqual(40, 5, 2.98);
            AddAngleEqual(45, 4, 2.73);   AddAngleEqual(45, 5, 3.37);
            AddAngleEqual(50, 3, 2.32);   AddAngleEqual(50, 4, 3.05);   AddAngleEqual(50, 5, 3.77);
            AddAngleEqual(50, 6, 4.47);   AddAngleEqual(50, 7, 5.15);   AddAngleEqual(50, 8, 5.80);
            AddAngleEqual(63, 4, 3.90);   AddAngleEqual(63, 5, 4.81);   AddAngleEqual(63, 6, 5.72);
            AddAngleEqual(70, 5, 5.38);   AddAngleEqual(70, 6, 6.39);   AddAngleEqual(70, 7, 7.39);
            AddAngleEqual(70, 8, 8.37);
            AddAngleEqual(75, 5, 5.80);   AddAngleEqual(75, 6, 6.89);   AddAngleEqual(75, 7, 7.96);
            AddAngleEqual(75, 8, 9.02);   AddAngleEqual(75, 9, 10.07);
            AddAngleEqual(80, 6, 7.36);   AddAngleEqual(80, 7, 8.51);   AddAngleEqual(80, 8, 9.65);
            AddAngleEqual(90, 6, 8.33);   AddAngleEqual(90, 7, 9.64);   AddAngleEqual(90, 8, 10.93);
            AddAngleEqual(90, 9, 12.20);
            AddAngleEqual(100, 7, 10.79); AddAngleEqual(100, 8, 12.25);
            AddAngleEqual(100, 10, 15.10); AddAngleEqual(100, 12, 17.90);
            AddAngleEqual(125, 8, 15.46); AddAngleEqual(125, 9, 17.30);
            AddAngleEqual(125, 10, 19.10); AddAngleEqual(125, 12, 22.68);
            AddAngleEqual(140, 9, 19.41); AddAngleEqual(140, 10, 21.45);
            AddAngleEqual(160, 10, 24.67); AddAngleEqual(160, 12, 29.35);
            AddAngleEqual(200, 12, 36.97); AddAngleEqual(200, 16, 48.65);

            // ---------- Уголок неравнополочный (ГОСТ 8510-86) ----------
            void AddAngleUnequal(double b, double a, double t, double kgm)
            {
                list.Add(new RolledProfile
                {
                    Kind = ProfileKind.AngleUnequal,
                    SizeCode = $"{b:0.##}x{a:0.##}x{t:0.##}",
                    GostDesignation = $"Уголок {b:0.##}×{a:0.##}×{t:0.##} ГОСТ 8510-86",
                    WeightPerMeterKg = kgm,
                    Height = b,
                    Width = a,
                    WallThickness = t,
                    CompatibleMachines = MA,
                });
            }
            AddAngleUnequal(25, 16, 3, 0.91);
            AddAngleUnequal(32, 20, 3, 1.17);
            AddAngleUnequal(40, 25, 3, 1.48);  AddAngleUnequal(40, 25, 4, 1.94);
            AddAngleUnequal(50, 32, 3, 1.90);  AddAngleUnequal(50, 32, 4, 2.40);
            AddAngleUnequal(63, 40, 4, 3.17);  AddAngleUnequal(63, 40, 5, 3.91);  AddAngleUnequal(63, 40, 6, 4.63);
            AddAngleUnequal(75, 50, 5, 4.79);  AddAngleUnequal(75, 50, 6, 5.69);  AddAngleUnequal(75, 50, 8, 7.43);
            AddAngleUnequal(100, 63, 6, 7.53); AddAngleUnequal(100, 63, 8, 9.87); AddAngleUnequal(100, 63, 10, 12.14);
            AddAngleUnequal(125, 80, 7, 11.04); AddAngleUnequal(125, 80, 10, 15.47);
            AddAngleUnequal(160, 100, 9, 18.00); AddAngleUnequal(160, 100, 12, 23.59);

            // ---------- Профтруба квадратная (ГОСТ 30245-2003) ----------
            void AddSquareTube(double side, double wall, double kgm)
            {
                list.Add(new RolledProfile
                {
                    Kind = ProfileKind.SquareTube,
                    SizeCode = $"{side:0.##}x{side:0.##}x{wall:0.##}",
                    GostDesignation = $"Профиль {side:0.##}×{side:0.##}×{wall:0.##} ГОСТ 30245-2003",
                    WeightPerMeterKg = kgm,
                    Height = side,
                    Width = side,
                    WallThickness = wall,
                    CompatibleMachines = MB,
                });
            }
            AddSquareTube(15, 1.5, 0.62);
            AddSquareTube(20, 1.5, 0.86); AddSquareTube(20, 2, 1.05);
            AddSquareTube(25, 1.5, 1.09); AddSquareTube(25, 2, 1.36);
            AddSquareTube(30, 2, 1.67);   AddSquareTube(30, 3, 2.36);
            AddSquareTube(40, 2, 2.30);   AddSquareTube(40, 3, 3.37); AddSquareTube(40, 4, 4.36);
            AddSquareTube(50, 2, 2.93);   AddSquareTube(50, 3, 4.32); AddSquareTube(50, 4, 5.63);
            AddSquareTube(60, 3, 5.26);   AddSquareTube(60, 4, 6.87); AddSquareTube(60, 5, 8.37);
            AddSquareTube(70, 3, 6.21);   AddSquareTube(70, 4, 8.13);
            AddSquareTube(80, 3, 7.15);   AddSquareTube(80, 4, 9.38); AddSquareTube(80, 5, 11.48);
            AddSquareTube(80, 6, 13.49);
            AddSquareTube(100, 4, 11.88); AddSquareTube(100, 5, 14.62); AddSquareTube(100, 6, 17.27);
            AddSquareTube(100, 8, 22.28);
            AddSquareTube(120, 4, 14.38); AddSquareTube(120, 5, 17.76); AddSquareTube(120, 6, 21.03);
            AddSquareTube(140, 5, 20.89); AddSquareTube(140, 6, 24.80);
            AddSquareTube(150, 5, 22.46); AddSquareTube(150, 6, 26.69);
            AddSquareTube(160, 6, 28.58);
            AddSquareTube(180, 6, 32.37); AddSquareTube(180, 8, 42.37);
            AddSquareTube(200, 6, 36.15); AddSquareTube(200, 8, 47.40);

            // ---------- Профтруба прямоугольная (ГОСТ 30245-2003) ----------
            void AddRectTube(double h, double w, double wall, double kgm)
            {
                list.Add(new RolledProfile
                {
                    Kind = ProfileKind.RectTube,
                    SizeCode = $"{h:0.##}x{w:0.##}x{wall:0.##}",
                    GostDesignation = $"Профиль {h:0.##}×{w:0.##}×{wall:0.##} ГОСТ 30245-2003",
                    WeightPerMeterKg = kgm,
                    Height = h,
                    Width = w,
                    WallThickness = wall,
                    CompatibleMachines = MB,
                });
            }
            AddRectTube(40, 20, 1.5, 1.30); AddRectTube(40, 20, 2, 1.70);
            AddRectTube(40, 25, 2, 1.90);
            AddRectTube(50, 25, 1.5, 1.66); AddRectTube(50, 25, 2, 2.18);
            AddRectTube(50, 30, 2, 2.36);   AddRectTube(50, 30, 3, 3.43);
            AddRectTube(60, 30, 2, 2.67);   AddRectTube(60, 30, 3, 3.90);
            AddRectTube(60, 40, 2, 2.98);   AddRectTube(60, 40, 3, 4.36); AddRectTube(60, 40, 4, 5.65);
            AddRectTube(80, 40, 2, 3.60);   AddRectTube(80, 40, 3, 5.30); AddRectTube(80, 40, 4, 6.87);
            AddRectTube(100, 50, 3, 6.71);  AddRectTube(100, 50, 4, 8.77); AddRectTube(100, 50, 5, 10.70);
            AddRectTube(100, 60, 3, 7.15);  AddRectTube(100, 60, 4, 9.38);
            AddRectTube(120, 60, 4, 10.63); AddRectTube(120, 60, 5, 13.05);
            AddRectTube(120, 80, 4, 11.88); AddRectTube(120, 80, 5, 14.62);
            AddRectTube(140, 60, 4, 11.88);
            AddRectTube(150, 100, 5, 18.93); AddRectTube(150, 100, 6, 22.46);
            AddRectTube(160, 80, 5, 17.76); AddRectTube(160, 80, 6, 21.03);
            AddRectTube(180, 100, 5, 21.29); AddRectTube(180, 100, 6, 25.29);
            AddRectTube(200, 100, 6, 27.13); AddRectTube(200, 100, 8, 35.11);

            // ---------- Швеллер (ГОСТ 8240-97), серия "У" ----------
            void AddChannel(string num, double h, double kgm)
            {
                list.Add(new RolledProfile
                {
                    Kind = ProfileKind.Channel,
                    SizeCode = "№" + num,
                    GostDesignation = $"Швеллер {num}У ГОСТ 8240-97",
                    WeightPerMeterKg = kgm,
                    Height = h,
                    CompatibleMachines = MB,
                });
            }
            AddChannel("5",  50,  4.84);
            AddChannel("6.5", 65, 5.90);
            AddChannel("8",  80,  7.05);
            AddChannel("10", 100, 8.59);
            AddChannel("12", 120, 10.40);
            AddChannel("14", 140, 12.30);
            AddChannel("16", 160, 14.20);
            AddChannel("18", 180, 16.30);
            AddChannel("20", 200, 18.40);
            AddChannel("22", 220, 21.00);
            AddChannel("24", 240, 24.00);
            AddChannel("27", 270, 27.70);
            AddChannel("30", 300, 31.80);

            // ---------- Двутавр (ГОСТ 8239-89) ----------
            void AddIBeam(string num, double h, double kgm)
            {
                list.Add(new RolledProfile
                {
                    Kind = ProfileKind.IBeam,
                    SizeCode = "№" + num,
                    GostDesignation = $"Двутавр {num} ГОСТ 8239-89",
                    WeightPerMeterKg = kgm,
                    Height = h,
                    CompatibleMachines = MB,
                });
            }
            AddIBeam("10", 100, 9.46);
            AddIBeam("12", 120, 11.50);
            AddIBeam("14", 140, 13.70);
            AddIBeam("16", 160, 15.90);
            AddIBeam("18", 180, 18.40);
            AddIBeam("20", 200, 21.00);
            AddIBeam("22", 220, 24.00);
            AddIBeam("24", 240, 27.30);
            AddIBeam("27", 270, 31.50);
            AddIBeam("30", 300, 36.50);

            return list;
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

        // ✅ НОВЫЕ МЕТОДЫ для СОРТАМЕНТА ПРОКАТА
        public List<RolledProfile> GetAllRolledProfiles()
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<RolledProfile>()
                         .OrderBy(p => p.Kind)
                         .ThenBy(p => p.WeightPerMeterKg)
                         .ToList();
            }
        }

        public List<RolledProfile> GetRolledProfilesByKind(ProfileKind kind)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Table<RolledProfile>()
                         .Where(p => p.Kind == kind)
                         .OrderBy(p => p.WeightPerMeterKg)
                         .ToList();
            }
        }

        public RolledProfile? GetRolledProfileById(int id)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                return db.Find<RolledProfile>(id);
            }
        }

        public int AddRolledProfile(RolledProfile profile)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Insert(profile);
                return profile.Id;
            }
        }

        public void UpdateRolledProfile(RolledProfile profile)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Update(profile);
            }
        }

        public void DeleteRolledProfile(int id)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.Delete<RolledProfile>(id);
            }
        }

        public void UpdateAllRolledProfiles(List<RolledProfile> list)
        {
            using (var db = new SQLiteConnection(_dbPath))
            {
                db.DeleteAll<RolledProfile>();
                db.InsertAll(list);
            }
        }
    }
}
