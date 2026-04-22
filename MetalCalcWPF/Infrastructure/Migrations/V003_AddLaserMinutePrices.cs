using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;
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
    ///    (они уже есть в DDL V001). Эта миграция тогда не делает ничего —
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

        public void Up(AppDbContext ctx)
        {
            const string table = "WorkshopSettings";

            if (!HasColumn(ctx, table, "LaserAirMinutePrice"))
            {
                ctx.Database.ExecuteSqlRaw(
                    "ALTER TABLE \"" + table + "\" ADD COLUMN \"LaserAirMinutePrice\" decimal NOT NULL DEFAULT 65");
            }

            if (!HasColumn(ctx, table, "LaserOxygenMinutePrice"))
            {
                ctx.Database.ExecuteSqlRaw(
                    "ALTER TABLE \"" + table + "\" ADD COLUMN \"LaserOxygenMinutePrice\" decimal NOT NULL DEFAULT 85");
            }
        }

        /// <summary>
        /// Проверяет через PRAGMA table_info (точнее, её табличную форму
        /// <c>pragma_table_info</c>), что колонка присутствует в таблице.
        /// Регистр имени колонки в SQLite не важен, сравниваем OrdinalIgnoreCase.
        /// </summary>
        private static bool HasColumn(AppDbContext ctx, string table, string column)
        {
            // SQLite не поддерживает параметризацию DDL/PRAGMA, поэтому имя таблицы
            // подставляется конкатенацией. Это безопасно: имена зашиты в коде миграций.
            var names = ctx.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info(\"" + table + "\")")
                .ToList();

            return names.Any(n => string.Equals(n, column, StringComparison.OrdinalIgnoreCase));
        }
    }
}
