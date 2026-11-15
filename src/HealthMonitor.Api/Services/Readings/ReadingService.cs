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

    public async Task<(IReadOnlyList<ReadingBatchResultDto>? Created, Dictionary<string, string[]>? Errors)> CreateBatchAsync(
        string crewId,
        ReadingBatchCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var valid = new List<(VitalThreshold Threshold, double Value)>();

        if (dto.RecordedAt is { } at && at.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            errors["recordedAt"] = ["Recorded time cannot be in the future."];

        foreach (var item in dto.Values)
        {
            if (item is null)
            {
                errors["values"] = ["Each value needs an indicator and a value."];
                continue;
            }

            var key = string.IsNullOrWhiteSpace(item.Indicator) ? "values" : item.Indicator;
            var threshold = vitals.Find(item.Indicator);
            if (threshold is null)
            {
                errors[key] = [$"Unknown indicator '{item.Indicator}'."];
                continue;
            }

            if (valid.Any(v => v.Threshold.Indicator == threshold.Indicator))
            {
                errors[threshold.Indicator] = [$"{threshold.Name} was sent twice."];
                continue;
            }

            if (item.Value is null)
            {
                errors[threshold.Indicator] = [$"{threshold.Name}: value is required."];
                continue;
            }

            if (VitalPlausibility.Validate(threshold, item.Value.Value) is { } message)
            {
                errors[threshold.Indicator] = [message];
                continue;
            }

            valid.Add((threshold, item.Value.Value));
        }

        if (errors.Count > 0)
            return (null, errors);

        var recordedAt = dto.RecordedAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var rows = valid
            .Select(v => (v.Threshold, Reading: new Reading
            {
                CrewId = crewId,
                Indicator = v.Threshold.Indicator,
                Value = v.Value,
                RecordedAt = recordedAt,
            }))
            .ToList();

        data.Readings.AddRange(rows.Select(r => r.Reading));
        await data.SaveChangesAsync(cancellationToken);

        IReadOnlyList<ReadingBatchResultDto> created = rows
            .Select(r =>
            {
                // Same status rule as the single endpoint.
                var status = vitals.GetStatus(r.Threshold, r.Reading.Value);
                return new ReadingBatchResultDto
                {
                    Id = r.Reading.Id,
                    CrewId = r.Reading.CrewId,
                    Indicator = r.Reading.Indicator,
                    Name = r.Threshold.Name,
                    Unit = r.Threshold.Unit,
                    Value = r.Reading.Value,
                    RecordedAt = r.Reading.RecordedAt,
                    Status = status,
                    Guidance = status switch
                    {
                        "ok" => r.Threshold.Guidance.Ok,
                        "warning" => r.Threshold.Guidance.Warning,
                        _ => r.Threshold.Guidance.Critical,
                    },
                    NasaRisk = r.Threshold.NasaRisk,
                };
            })
            .ToList();

        return (created, null);
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
