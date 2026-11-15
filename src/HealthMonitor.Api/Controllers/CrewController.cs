namespace HealthMonitor.Api.Controllers;

using Auth;
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CrewSummaryDto
{
    public string CrewId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public bool HasVitals { get; init; }

    public bool HasBlood { get; init; }

    public bool HasNutrition { get; init; }
}

/// <summary>Crew roster with data availability. Flight surgeons only.</summary>
[ApiController]
[Authorize(Policy = Policies.FlightSurgeon)]
[Route("api/[controller]")]
public class CrewController(
    AppDbContext data) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CrewSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CrewSummaryDto>>> Get(
        CancellationToken cancellationToken)
    {
        var astronauts = await data
            .Users
            .AsNoTracking()
            .Where(u => u.Role == Roles.Astronaut && u.CrewId != null)
            .OrderBy(u => u.CrewId)
            .Select(u => new { CrewId = u.CrewId!, u.DisplayName })
            .ToListAsync(cancellationToken);

        var withVitals = (await data.Readings
                .Select(r => r.CrewId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var withBlood = (await data.BloodPanelResults
                .Select(r => r.CrewId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var withNutrition = (await data.NutritionDays
                .Select(d => d.CrewId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<CrewSummaryDto> result = astronauts
            .Select(a => new CrewSummaryDto
            {
                CrewId = a.CrewId,
                DisplayName = a.DisplayName,
                HasVitals = withVitals.Contains(a.CrewId),
                HasBlood = withBlood.Contains(a.CrewId),
                HasNutrition = withNutrition.Contains(a.CrewId),
            })
            .ToList();

        return this.Ok(result);
    }
}
