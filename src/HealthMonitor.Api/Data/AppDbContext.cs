namespace HealthMonitor.Api.Data;

using Microsoft.EntityFrameworkCore;
using Models.Entities;

public class AppDbContext(
    DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Sample> Samples { get; init; }

    public DbSet<BloodPanelResult> BloodPanelResults { get; init; }

    public DbSet<Reading> Readings { get; init; }

    public DbSet<User> Users { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<Sample>(entity =>
            {
                entity
                    .HasKey(s => s.Id);

                entity
                    .Property(s => s.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity
                    .Property(s => s.Unit)
                    .HasMaxLength(20);
            })
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
