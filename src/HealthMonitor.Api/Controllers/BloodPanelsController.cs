namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.BloodPanels;

/// <summary>
/// Blood panels. Astronauts: own crewId only. Flight surgeons: crewId required.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BloodPanelsController(
    IBloodPanelService bloodPanels,
    ICrewAccessService crewAccess) : ControllerBase
{
    /// <summary>Crew ids with blood data: all for flight surgeons, only your own for astronauts.</summary>
    [HttpGet("crew")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCrewIds(
        CancellationToken cancellationToken)
    {
        var crewIds = await bloodPanels.GetCrewIdsAsync(cancellationToken);

        if (this.User.IsInRole(Roles.FlightSurgeon))
            return this.Ok(crewIds);

        var access = await crewAccess.ResolveAsync(this.User, null, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        IReadOnlyList<string> own = crewIds
            .Where(id => id == access.CrewId)
            .ToList();

        return this.Ok(own);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BloodPanelResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BloodPanelResponseDto>>> GetByCrew(
        [FromQuery] string? crewId,
        [FromQuery] string? panel = null,
        CancellationToken cancellationToken = default)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var results = await bloodPanels.GetByCrewAsync(
            access.CrewId!,
            panel,
            cancellationToken);

        return this.Ok(results);
    }
}
