namespace HealthMonitor.Api.Data;

using Microsoft.EntityFrameworkCore;
using Models.Entities;

public class AppDbContext(
    DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Sample> Samples { get; init; }

    public DbSet<BloodPanelResult> BloodPanelResults { get; init; }

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
            });
}
