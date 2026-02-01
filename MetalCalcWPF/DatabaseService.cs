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
            // При создании сервиса сразу проверяем, есть ли файл БД. Если нет - создаем.
            using (var db = new SQLiteConnection(_dbPath))
            {
                // Эта строка создает таблицу, если её нет. Если есть - ничего не портит.
                db.CreateTable<WorkshopSettings>();
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
    }
}