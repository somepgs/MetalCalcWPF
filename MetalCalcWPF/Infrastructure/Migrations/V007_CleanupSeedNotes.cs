using Microsoft.EntityFrameworkCore;
using MetalCalcWPF.Infrastructure.Persistence;

namespace MetalCalcWPF.Infrastructure.Migrations
{
    /// <summary>
    /// v7 — одноразовая чистка технических подписей в Notes у цехов, засеянных V005.
    ///
    /// <para>В первой версии V005 в Notes писалось «Создан миграцией v5.», что увидел
    /// конечный пользователь при открытии редактора БД и справедливо посчитал шумом
    /// (поле Notes — пользовательское, для заметок типа «договор № 123, ответственный Иванов»).
    /// V005 теперь сидит с пустым Notes; этот шаг чистит уже выставленный текст
    /// у тех, кто обновляется поверх существующей БД.</para>
    ///
    /// <para>UPDATE безопасен: правит только цеха, у которых Notes СОДЕРЖИТ технический
    /// маркер «миграцией v5». Если пользователь успел дописать туда что-то своё —
    /// редактируем мы только эту фразу не трогаем (запись не подходит под фильтр).</para>
    /// </summary>
    public class V007_CleanupSeedNotes : IMigration
    {
        public int Version => 7;

        public string Description =>
            "Чистка технических подписей в Workshop.Notes от V005 (одноразовая).";

        public void Up(AppDbContext ctx)
        {
            // Точные строки, которые писались в первой редакции V005 — заменяем на пустые.
            // LIKE с шаблоном «%миграцией v5%» поймает «Создан миграцией v5.» и
            // «Наш цех. Создан миграцией v5.» — оба варианта исходного сида.
            ctx.Database.ExecuteSqlRaw(@"
                UPDATE ""Workshop""
                SET ""Notes"" = CASE
                    WHEN ""Notes"" = 'Создан миграцией v5.' THEN ''
                    WHEN ""Notes"" = 'Наш цех. Создан миграцией v5.' THEN 'Наш цех'
                    ELSE ""Notes""
                END
                WHERE ""Notes"" LIKE '%миграцией v5%'");
        }
    }
}
