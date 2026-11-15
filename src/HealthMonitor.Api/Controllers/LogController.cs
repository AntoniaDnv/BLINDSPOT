namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Logging;

/// <summary>Log and Data Entry helpers. Astronauts: own crewId only. Flight surgeons: crewId required.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LogController(
    ICrewLogService log,
    ICrewAccessService crewAccess) : ControllerBase
{
    /// <summary>Newest entries across vitals, symptoms, nutrition and medications (default 10, max 50).</summary>
    [HttpGet("recent")]
    [ProducesResponseType<IReadOnlyList<RecentEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RecentEntryDto>>> Recent(
        [FromQuery] string? crewId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        return this.Ok(await log.GetRecentAsync(access.CrewId!, limit, cancellationToken));
    }
}
