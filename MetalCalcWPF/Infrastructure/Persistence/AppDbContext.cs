using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core DbContext для приложения.
    ///
    /// <para>Главная цель маппинга — бинарная совместимость со схемой, которую
    /// исторически создавал <c>sqlite-net-pcl</c>. Все существующие файлы
    /// <c>workshop.db</c> у пользователей должны продолжать открываться и читаться
    /// без пересоздания и без потери данных. Поэтому здесь:</para>
    ///
    /// <list type="bullet">
    ///   <item>имена таблиц явно заданы через <c>ToTable("ClassName")</c> — sqlite-net
    ///     использовал имя класса, EF Core по умолчанию плюрализует («RolledProfiles»),
    ///     что несовместимо с существующей схемой;</item>
    ///   <item>все <see cref="DateTime"/> хранятся как <c>long</c>-тики через value
    ///     converter — это поведение <c>SQLiteConnection.StoreDateTimeAsTicks = true</c>
    ///     (дефолт sqlite-net), значит поле <c>SchemaVersion.AppliedAt</c> и
    ///     <c>OrderHistory.CreatedDate</c> в существующих БД имеют именно такой формат;</item>
    ///   <item>для <see cref="RolledProfile"/> исключены вычисляемые флаги совместимости
    ///     (<c>CanLaser</c>, <c>CanBandSaw</c> и т.п.) — раньше они были помечены
    ///     <c>[Ignore]</c> у sqlite-net;</item>
    ///   <item><see cref="SchemaVersion.Version"/> помечен как PK без автоинкремента —
    ///     версии миграций задаёт <c>MigrationRunner</c> вручную.</item>
    /// </list>
    ///
    /// <para>Контекст создаётся и уничтожается на каждый вызов <c>DatabaseService</c> —
    /// так же, как раньше создавался <c>SQLiteConnection</c>. Это не создаёт
    /// заметной нагрузки, но избавляет от проблем с разделением контекста между
    /// потоками и долгоживущих change tracker-ов.</para>
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<WorkshopSettings> WorkshopSettings => Set<WorkshopSettings>();
        public DbSet<MaterialType> MaterialTypes => Set<MaterialType>();
        public DbSet<MaterialProfile> MaterialProfiles => Set<MaterialProfile>();
        public DbSet<BendingProfile> BendingProfiles => Set<BendingProfile>();
        public DbSet<WeldingProfile> WeldingProfiles => Set<WeldingProfile>();
        public DbSet<RolledProfile> RolledProfiles => Set<RolledProfile>();
        public DbSet<OrderHistory> OrderHistory => Set<OrderHistory>();
        public DbSet<CuttingMachine> CuttingMachines => Set<CuttingMachine>();
        public DbSet<Workshop> Workshops => Set<Workshop>();
        public DbSet<Person> Persons => Set<Person>();
        public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            // DateTime <-> long ticks — совместимость с sqlite-net-pcl (StoreDateTimeAsTicks=true).
            var ticksConverter = new ValueConverter<DateTime, long>(
                v => v.Ticks,
                v => new DateTime(v));

            foreach (var entityType in mb.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                        property.SetValueConverter(ticksConverter);
                }
            }

            mb.Entity<WorkshopSettings>(e =>
            {
                e.ToTable("WorkshopSettings");
                e.HasKey(x => x.Id);
            });

            mb.Entity<MaterialType>(e =>
            {
                e.ToTable("MaterialType");
                e.HasKey(x => x.Id);
            });

            mb.Entity<MaterialProfile>(e =>
            {
                e.ToTable("MaterialProfile");
                e.HasKey(x => x.Id);
            });

            mb.Entity<BendingProfile>(e =>
            {
                e.ToTable("BendingProfile");
                e.HasKey(x => x.Id);
            });

            mb.Entity<WeldingProfile>(e =>
            {
                e.ToTable("WeldingProfile");
                e.HasKey(x => x.Id);
            });

            mb.Entity<RolledProfile>(e =>
            {
                e.ToTable("RolledProfile");
                e.HasKey(x => x.Id);

                // Вычисляемые булевы флаги — не колонки БД, это обёртки над CompatibleMachines.
                // Раньше помечались атрибутом [Ignore] от sqlite-net.
                e.Ignore(x => x.CanLaser);
                e.Ignore(x => x.CanBandSaw);
                e.Ignore(x => x.CanPressShears);
                e.Ignore(x => x.CanGuillotine);
                e.Ignore(x => x.CanAngleGrinder);
            });

            mb.Entity<OrderHistory>(e =>
            {
                e.ToTable("OrderHistory");
                e.HasKey(x => x.Id);

                // sqlite-net по умолчанию делает все строки nullable (без [NotNull]).
                // Сохраняем такую же семантику, чтобы legacy-строки с NULL читались без ошибок.
                e.Property(x => x.ClientName).IsRequired(false);
                e.Property(x => x.Description).IsRequired(false);
                e.Property(x => x.OperationType).IsRequired(false);
            });

            mb.Entity<CuttingMachine>(e =>
            {
                e.ToTable("CuttingMachine");
                e.HasKey(x => x.Id);
            });

            mb.Entity<Workshop>(e =>
            {
                e.ToTable("Workshop");
                e.HasKey(x => x.Id);
            });

            mb.Entity<Person>(e =>
            {
                e.ToTable("Person");
                e.HasKey(x => x.Id);

                // FK не enforce-им через Restrict/Cascade на уровне БД: справочники
                // редактируются вручную, цеха могут переименовываться/удаляться
                // быстрее, чем заявители. Целостность поддерживается приложением
                // (валидация при сохранении заказа).
                e.HasOne<Workshop>()
                 .WithMany()
                 .HasForeignKey(p => p.WorkshopId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<SchemaVersion>(e =>
            {
                e.ToTable("SchemaVersion");
                e.HasKey(x => x.Version);
                // Версия задаётся миграциями вручную — никакого автоинкремента.
                e.Property(x => x.Version).ValueGeneratedNever();
            });
        }
    }
}
