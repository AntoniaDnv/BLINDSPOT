namespace HealthMonitor.Api.Services.Readings;

using Data;
using Microsoft.EntityFrameworkCore;
using Models.Dtos;
using Models.Entities;
using Models.Vitals;
using Services.Vitals;

public class ReadingService(
    AppDbContext data,
    IVitalThresholdService vitals) : IReadingService
{
    public async Task<ReadingResponseDto?> CreateAsync(
        string crewId,
        ReadingCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var threshold = vitals.Find(dto.Indicator);
        if (threshold is null || dto.Value is null)
            return null;

        var reading = new Reading
        {
            CrewId = crewId,
            Indicator = threshold.Indicator,
            Value = dto.Value.Value,
            RecordedAt = dto.RecordedAt?.ToUniversalTime() ?? DateTime.UtcNow,
        };

        data.Readings.Add(reading);
        await data.SaveChangesAsync(cancellationToken);

        return ToDto(reading, threshold);
    }

    public async Task<IReadOnlyList<ReadingResponseDto>> GetHistoryAsync(
        string crewId,
        string? indicator,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Reading> query = data
            .Readings
            .AsNoTracking()
            .Where(r => r.CrewId == crewId);

        if (!string.IsNullOrWhiteSpace(indicator))
        {
            var threshold = vitals.Find(indicator);
            if (threshold is null)
                return [];

            query = query.Where(r => r.Indicator == threshold.Indicator);
        }

        var rows = await query
            .OrderBy(r => r.RecordedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => ToDto(r, vitals.Find(r.Indicator)))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<LatestReadingResponseDto>> GetLatestAsync(
        string crewId,
        CancellationToken cancellationToken = default)
    {
        var readings = await data
            .Readings
            .AsNoTracking()
            .Where(r => r.CrewId == crewId)
            .ToListAsync(cancellationToken);

        var latestByIndicator = readings
            .GroupBy(r => r.Indicator)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(r => r.RecordedAt)
                    .ThenByDescending(r => r.Id)
                    .First());

        var result = new List<LatestReadingResponseDto>();

        foreach (var threshold in vitals.GetAll())
        {
            if (!latestByIndicator.TryGetValue(threshold.Indicator, out var reading))
                continue;

            result.Add(new LatestReadingResponseDto
            {
                Id = reading.Id,
                CrewId = reading.CrewId,
                Indicator = reading.Indicator,
                Name = threshold.Name,
                Unit = threshold.Unit,
                Value = reading.Value,
                RecordedAt = reading.RecordedAt,
                Status = vitals.GetStatus(threshold, reading.Value),
                NasaRisk = threshold.NasaRisk,
                Guidance = threshold.Guidance,
            });
        }

        return result.AsReadOnly();
    }

    private ReadingResponseDto ToDto(
        Reading reading,
        VitalThreshold? threshold)
        => new()
        {
            Id = reading.Id,
            CrewId = reading.CrewId,
            Indicator = reading.Indicator,
            Value = reading.Value,
            RecordedAt = reading.RecordedAt,
            Status = threshold is null
                ? "unknown"
                : vitals.GetStatus(threshold, reading.Value),
        };
}
