namespace HealthMonitor.Api.Services.BloodPanels;

using System.Text.Json;
using Data.Seeding;
using Models.Dtos;
using Models.Seed;

public class BloodPanelService(
    JsonLoader jsonLoader,
    ILogger<BloodPanelService> logger)
    : IBloodPanelService
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private List<BloodPanelSeedRow>? _cache;

    private async Task<List<BloodPanelSeedRow>> LoadDataAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
            return _cache;

        var data = await jsonLoader.Load<BloodPanelSeedRow>(
            "blood-panels.json",
            cancellationToken);

        _cache = data;

        logger.LogInformation(
            "Loaded {Count} blood panel rows",
            data.Count);

        return data;
    }

    public async Task<IReadOnlyList<string>> GetCrewIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken);

        return data
            .Select(r => r.CrewId)
            .Distinct()
            .OrderBy(id => id)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<BloodPanelResponseDto>> GetByCrewAsync(
        string crewId,
        string? panel = null,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken);

        var query = data
            .Where(r => r.CrewId == crewId);

        if (!string.IsNullOrWhiteSpace(panel))
            query = query.Where(r => r.Panel == panel);

        var results = query
            .OrderBy(r => r.DayOffset)
            .ThenBy(r => r.Analyte)
            .Select(ToDto)
            .ToList();

        return results.AsReadOnly();
    }

    private static BloodPanelResponseDto ToDto(BloodPanelSeedRow row)
    {
        var status = ComputeStatus(row);

        return new BloodPanelResponseDto
        {
            CrewId = row.CrewId,
            Panel = row.Panel,
            Analyte = row.Analyte,
            Timepoint = row.Timepoint,
            DayOffset = row.DayOffset,
            Value = row.Value,
            Unit = row.Unit,
            RangeMin = row.RangeMin,
            RangeMax = row.RangeMax,
            Status = status,
            Flag = row.Flag,
        };
    }

    private static string ComputeStatus(BloodPanelSeedRow row)
    {
        // flagged takes priority — it's a data-quality note
        if (!string.IsNullOrWhiteSpace(row.Flag))
            return "flagged";

        // unknown if no range bounds are available
        if (!row.RangeMin.HasValue || !row.RangeMax.HasValue)
            return "unknown";

        // ok if inside range (inclusive)
        if (row.Value >= row.RangeMin.Value && row.Value <= row.RangeMax.Value)
            return "ok";

        // warning if outside range
        return "warning";
    }
}
