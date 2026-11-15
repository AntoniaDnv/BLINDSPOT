namespace HealthMonitor.Api.Data.Seeding;

using Api.Auth;
using Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Entities;
using Models.Seed;

public class DataSeeder(
    AppDbContext data,
    JsonLoader jsonLoader,
    CsvLoader csvLoader,
    IPasswordHasher<User> passwordHasher,
    IOptions<SeedingOptions> options,
    ILogger<DataSeeder> logger)
{
    public const string SimulatedReadingsCrewId = "C001";

    private readonly SeedingOptions options = options.Value;

    public async Task Seed(
        CancellationToken cancellationToken = default)
    {
        await SeedSamplesAsync(cancellationToken);
        await SeedBloodPanelsAsync(cancellationToken);
        await SeedReadingsAsync(cancellationToken);
        await SeedUsersAsync(cancellationToken);
        //add more seeders here.
    }

    private async Task SeedSamplesAsync(
        CancellationToken cancellationToken = default)
    {
        if (await data.Samples.AnyAsync(cancellationToken))
            return;

        var rows = new List<SampleSeedRow>();

        if (this.options.UseJson)
        {
            var json = await jsonLoader.Load<SampleSeedRow>(
                this.options.JsonFile,
                cancellationToken);

            rows.AddRange(json);
        }

        if (this.options.UseCsv)
        {
            var csv = await csvLoader.Load<SampleSeedRow>(
                this.options.CsvFile,
                cancellationToken);

            rows.AddRange(csv);
        }

        var entities = rows
            .Select(r => new Sample
            {
                Name = r.Name,
                Value = r.Value,
                Unit = r.Unit,
                RecordedAt = r.RecordedAt,
            });

        data.AddRange(entities);
        var saved = await data.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Count} samples (json: {Json}, csv: {Csv})",
            saved,
            this.options.UseJson,
            this.options.UseCsv);
    }

    private async Task SeedBloodPanelsAsync(
        CancellationToken cancellationToken = default)
    {
        if (await data.BloodPanelResults.AnyAsync(cancellationToken))
            return;

        var rows = await jsonLoader.Load<BloodPanelSeedRow>(
            "blood-panels.json",
            cancellationToken);

        var entities = rows
            .Select(r => new BloodPanelResult
            {
                CrewId = r.CrewId,
                Timepoint = r.Timepoint,
                DayOffset = r.DayOffset,
                Panel = r.Panel,
                Analyte = r.Analyte,
                Value = r.Value,
                Unit = r.Unit,
                RangeMin = r.RangeMin,
                RangeMax = r.RangeMax,
                Flag = r.Flag,
            });

        data.AddRange(entities);
        var saved = await data.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Count} blood panel results",
            saved);
    }

    private async Task SeedReadingsAsync(
        CancellationToken cancellationToken = default)
    {
        if (await data.Readings.AnyAsync(cancellationToken))
            return;

        var rows = await jsonLoader.Load<ReadingSeedRow>(
            "readings.json",
            cancellationToken);

        var entities = rows
            .Select(r => new Reading
            {
                // The simulated week of vitals belongs to crew member C001.
                CrewId = SimulatedReadingsCrewId,
                Indicator = r.Indicator,
                Value = r.Value,
                RecordedAt = r.RecordedAt,
            });

        data.AddRange(entities);
        var saved = await data.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Count} readings",
            saved);
    }

    private async Task SeedUsersAsync(
        CancellationToken cancellationToken = default)
    {
        if (await data.Users.AnyAsync(cancellationToken))
            return;

        var rows = await jsonLoader.Load<DemoUserSeedRow>(
            "demo-users.json",
            cancellationToken);

        foreach (var row in rows)
        {
            if (row.Role is not (Roles.Astronaut or Roles.FlightSurgeon))
            {
                logger.LogWarning("Skipping demo user with unknown role {Role}", row.Role);
                continue;
            }

            var user = new User
            {
                Email = row.Email.Trim(),
                NormalizedEmail = AuthService.NormalizeEmail(row.Email),
                DisplayName = row.DisplayName.Trim(),
                Role = row.Role,
                CrewId = row.Role == Roles.Astronaut ? row.CrewId?.Trim().ToUpperInvariant() : null,
                CreatedAt = DateTime.UtcNow,
            };

            // Hashed here; the plaintext test password is never stored or logged.
            user.PasswordHash = passwordHasher.HashPassword(user, row.Password);
            data.Users.Add(user);
        }

        var saved = await data.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Count} demo users",
            saved);
    }
}
