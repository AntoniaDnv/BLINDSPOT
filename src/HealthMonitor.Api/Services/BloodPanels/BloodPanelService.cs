namespace HealthMonitor.Api.Services.BloodPanels;

using Data;
using Microsoft.EntityFrameworkCore;
using Models.Dtos;

public class BloodPanelService(AppDbContext data) : IBloodPanelService
{
    public async Task<IReadOnlyList<string>> GetCrewIdsAsync(
        CancellationToken cancellationToken = default)
        => await data
            .BloodPanelResults
            .Select(r => r.CrewId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloodPanelResponseDto>> GetByCrewAsync(
        string crewId,
        string? panel = null,
        CancellationToken cancellationToken = default)
    {
        var query = data
            .BloodPanelResults
            .AsNoTracking()
            .Where(r => r.CrewId == crewId);

        if (!string.IsNullOrWhiteSpace(panel))
            query = query.Where(r => r.Panel == panel);

        var results = await query
            .OrderBy(r => r.DayOffset)
            .ThenBy(r => r.Analyte)
            .ToListAsync(cancellationToken);

        return results
            .Select(r => ToDto(r))
            .ToList()
            .AsReadOnly();
    }

    private static BloodPanelResponseDto ToDto(Models.Entities.BloodPanelResult result)
        => new()
        {
            CrewId = result.CrewId,
            Panel = result.Panel,
            Analyte = result.Analyte,
            Timepoint = result.Timepoint,
            DayOffset = result.DayOffset,
            Value = result.Value,
            Unit = result.Unit,
            RangeMin = result.RangeMin,
            RangeMax = result.RangeMax,
            Status = ComputeStatus(result),
            Flag = result.Flag,
        };

    private static string ComputeStatus(Models.Entities.BloodPanelResult result)
    {
        // flagged takes priority — it's a data-quality note
        if (!string.IsNullOrWhiteSpace(result.Flag))
            return "flagged";

        // unknown if no range bounds are available
        if (!result.RangeMin.HasValue || !result.RangeMax.HasValue)
            return "unknown";

        // ok if inside range (inclusive)
        if (result.Value >= result.RangeMin.Value && result.Value <= result.RangeMax.Value)
            return "ok";

        // warning if outside range
        return "warning";
    }
}
