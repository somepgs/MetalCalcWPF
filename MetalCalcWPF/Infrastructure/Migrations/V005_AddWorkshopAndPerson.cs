using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v5 — справочники цехов и людей для workflow заказов.
    ///
    /// Что делает:
    /// 1) Создаёт таблицы <c>Workshop</c> и <c>Person</c> (CREATE TABLE IF NOT EXISTS).
    ///    На свежей БД V001 уже создал их — этот шаг будет no-op.
    /// 2) Если <c>Workshop</c> пустая — засевает 5 внутренних цехов предприятия:
    ///    СВ, СК (столбы ЖБИ), ХЭСС (брусчатка/бордюры), сэндвич-панели, металлообработка.
    ///
    /// Persons не сидируем — их добавляет пользователь по мере появления заявителей
    /// и приёмщиков (мастера, бригадиры, ПТО). Внешних клиентов в Workshop тоже не
    /// добавляем — это контракт «по факту».
    ///
    /// Стратегия совместимости — как в V002:
    /// - повторный прогон не дублирует записи (проверка <c>Any()</c> на пустоту);
    /// - на legacy БД таблицы создаются с нуля.
    /// </summary>
    public class V005_AddWorkshopAndPerson : IMigration
    {
        public int Version => 5;

        public string Description =>
            "Добавлены справочники Workshop и Person + сид 5 внутренних цехов предприятия.";

        public void Up(AppDbContext ctx)
        {
            ctx.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""Workshop"" (
    ""Id"" integer primary key autoincrement not null,
    ""Name"" varchar,
    ""Kind"" integer not null,
    ""IsActive"" integer not null,
    ""Notes"" varchar
)");

            ctx.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""Person"" (
    ""Id"" integer primary key autoincrement not null,
    ""FullName"" varchar,
    ""Position"" varchar,
    ""WorkshopId"" integer,
    ""CanSubmit"" integer not null,
    ""CanAccept"" integer not null,
    ""IsActive"" integer not null,
    ""Notes"" varchar
)");

            // Сид внутренних цехов — только если справочник пустой.
            // Сценарий «уже добавил руками» уважается без слепого повторного посева.
            if (ctx.Workshops.AsNoTracking().Any())
                return;

            ctx.Workshops.AddRange(
                new Workshop { Name = "Цех СВ",                                Kind = WorkshopKind.Internal, IsActive = true,
                               Notes = "Создан миграцией v5." },
                new Workshop { Name = "Цех СК (столбы ЖБИ)",                    Kind = WorkshopKind.Internal, IsActive = true,
                               Notes = "Создан миграцией v5." },
                new Workshop { Name = "Цех ХЭСС (брусчатка и бордюры)",         Kind = WorkshopKind.Internal, IsActive = true,
                               Notes = "Создан миграцией v5." },
                new Workshop { Name = "Цех сэндвич-панелей",                    Kind = WorkshopKind.Internal, IsActive = true,
                               Notes = "Создан миграцией v5." },
                new Workshop { Name = "Цех металлообработки",                   Kind = WorkshopKind.Internal, IsActive = true,
                               Notes = "Наш цех. Создан миграцией v5." }
            );
            ctx.SaveChanges();
        }
    }
}
