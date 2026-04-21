using SQLite;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v1 — начальная схема БД. Все существующие таблицы на момент внедрения миграций.
    ///
    /// Использует <c>CreateTable</c> (= <c>CREATE TABLE IF NOT EXISTS</c>), поэтому
    /// безопасно выполняется на существующих установках: если таблицы уже созданы
    /// старой версией приложения, миграция не делает ничего и просто регистрирует v1
    /// как применённую.
    /// </summary>
    public class V001_InitialSchema : IMigration
    {
        public int Version => 1;

        public string Description => "Начальная схема: настройки, материалы, профили лазера/гибки/сварки, сортамент, история заказов.";

        public void Up(SQLiteConnection db)
        {
            db.CreateTable<WorkshopSettings>();
            db.CreateTable<MaterialType>();
            db.CreateTable<MaterialProfile>();
            db.CreateTable<BendingProfile>();
            db.CreateTable<WeldingProfile>();
            db.CreateTable<RolledProfile>();
            db.CreateTable<OrderHistory>();
        }
    }
}
