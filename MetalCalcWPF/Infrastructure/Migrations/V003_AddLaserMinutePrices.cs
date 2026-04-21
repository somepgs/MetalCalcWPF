using System;
using System.Linq;
using SQLite;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v3 — добавляет в WorkshopSettings два поля «цена минуты работы лазера»:
    /// <see cref="WorkshopSettings.LaserAirMinutePrice"/> (по умолчанию 65 тг/мин, воздух)
    /// и <see cref="WorkshopSettings.LaserOxygenMinutePrice"/> (85 тг/мин, кислород).
    ///
    /// Зачем: в Excel-исходнике цена минуты — **зашитая вручную цифра** (Справочник!B9/B10),
    /// куда уже включены зарплата, электричество, газ, расходники и амортизация.
    /// Формула «Себестоимость метра = Цена минуты / Скорость» даёт тот же результат,
    /// что мы видим в листе «Лазер bodor». Без этого поля
    /// <see cref="Services.Calculation.LaserCostCalculator"/> не сможет воспроизвести Excel.
    ///
    /// Стратегия:
    /// 1. На СВЕЖЕЙ БД: v1 создаёт таблицу WorkshopSettings с новыми колонками
    ///    (они уже есть в модели). Эта миграция тогда не делает ничего —
    ///    HasColumn вернёт true и ALTER TABLE не выполнится.
    /// 2. На СУЩЕСТВУЮЩЕЙ БД: v1 = CREATE TABLE IF NOT EXISTS — таблица не пересоздаётся,
    ///    новых колонок нет. Эта миграция добавит их через ALTER TABLE со значениями
    ///    по умолчанию 65 / 85, так что у пользователя сразу появятся рабочие цифры.
    ///
    /// ALTER TABLE ADD COLUMN в SQLite не ломает существующие данные и безопасен.
    /// </summary>
    public class V003_AddLaserMinutePrices : IMigration
    {
        public int Version => 3;

        public string Description =>
            "Добавлены цены минуты работы лазера (воздух/кислород) для Excel-паритета.";

        public void Up(SQLiteConnection db)
        {
            // Имя таблицы, как его выдаёт sqlite-net (по имени класса).
            const string table = "WorkshopSettings";

            if (!HasColumn(db, table, "LaserAirMinutePrice"))
            {
                db.Execute(
                    "ALTER TABLE \"" + table + "\" ADD COLUMN \"LaserAirMinutePrice\" decimal NOT NULL DEFAULT 65");
            }

            if (!HasColumn(db, table, "LaserOxygenMinutePrice"))
            {
                db.Execute(
                    "ALTER TABLE \"" + table + "\" ADD COLUMN \"LaserOxygenMinutePrice\" decimal NOT NULL DEFAULT 85");
            }
        }

        /// <summary>
        /// Проверяет через PRAGMA table_info, что колонка присутствует в таблице.
        /// Регистр имени колонки в SQLite не важен, сравниваем OrdinalIgnoreCase.
        /// </summary>
        private static bool HasColumn(SQLiteConnection db, string table, string column)
        {
            var rows = db.Query<TableInfoRow>("PRAGMA table_info(\"" + table + "\")");
            return rows.Any(r => string.Equals(r.name, column, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>DTO для десериализации строки PRAGMA table_info (нужно только поле name).</summary>
        private class TableInfoRow
        {
            public int cid { get; set; }
            public string name { get; set; } = string.Empty;
            public string type { get; set; } = string.Empty;
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }
    }
}
