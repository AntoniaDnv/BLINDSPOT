namespace HealthMonitor.Api.Data;

using Microsoft.EntityFrameworkCore;
using Models.Entities;

public class AppDbContext(
    DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BloodPanelResult> BloodPanelResults { get; init; }

    public DbSet<Reading> Readings { get; init; }

    public DbSet<User> Users { get; init; }

    public DbSet<NutritionDay> NutritionDays { get; init; }

    public DbSet<NutritionLogEntry> NutritionLogEntries { get; init; }

    public DbSet<MedicationLog> MedicationLogs { get; init; }

    public DbSet<SymptomLog> SymptomLogs { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<BloodPanelResult>(entity =>
            {
                entity
                    .HasKey(r => r.Id);

                entity
                    .Property(r => r.CrewId)
                    .IsRequired()
                    .HasMaxLength(20);

                entity
                    .Property(r => r.Timepoint)
                    .IsRequired()
                    .HasMaxLength(20);

                entity
                    .Property(r => r.Panel)
                    .IsRequired()
                    .HasMaxLength(10);

                entity
                    .Property(r => r.Analyte)
                    .IsRequired()
                    .HasMaxLength(100);

                entity
                    .Property(r => r.Unit)
                    .IsRequired()
                    .HasMaxLength(20);

                entity
                    .Property(r => r.Flag)
                    .HasMaxLength(50);
            })
            .Entity<Reading>(entity =>
            {
                entity
                    .HasKey(r => r.Id);

                entity
                    .Property(r => r.Indicator)
                    .IsRequired()
                    .HasMaxLength(50);

                entity
                    .Property(r => r.CrewId)
                    .IsRequired()
                    .HasMaxLength(20);

                entity
                    .HasIndex(r => new { r.CrewId, r.Indicator, r.RecordedAt });
            })
            .Entity<NutritionDay>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.CrewId).IsRequired().HasMaxLength(20);
                entity.HasIndex(d => new { d.CrewId, d.Day });
            })
            .Entity<NutritionLogEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CrewId).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Meal).HasMaxLength(100);
            })
            .Entity<MedicationLog>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.CrewId).IsRequired().HasMaxLength(20);
                entity.Property(m => m.Medication).IsRequired().HasMaxLength(80);
                entity.Property(m => m.Dose).IsRequired().HasMaxLength(40);
                entity.Property(m => m.Time).HasMaxLength(5);
            })
            .Entity<SymptomLog>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CrewId).IsRequired().HasMaxLength(20);
                entity.Property(s => s.Notes).HasMaxLength(1000);
            })
            .Entity<User>(entity =>
            {
                entity
                    .HasKey(u => u.Id);

                // Note: the InMemory provider does not enforce unique indexes;
                // AuthService checks uniqueness before inserting.
                entity
                    .HasIndex(u => u.NormalizedEmail)
                    .IsUnique();

                entity
                    .HasIndex(u => u.CrewId)
                    .IsUnique();

                entity
                    .Property(u => u.Email)
                    .IsRequired()
                    .HasMaxLength(254);

                entity
                    .Property(u => u.NormalizedEmail)
                    .IsRequired()
                    .HasMaxLength(254);

                entity
                    .Property(u => u.DisplayName)
                    .IsRequired()
                    .HasMaxLength(60);

                entity
                    .Property(u => u.PasswordHash)
                    .IsRequired();

                entity
                    .Property(u => u.Role)
                    .IsRequired()
                    .HasMaxLength(20);

                entity
                    .Property(u => u.CrewId)
                    .HasMaxLength(20);
            });
}
