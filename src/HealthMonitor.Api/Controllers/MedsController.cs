namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Logging;

/// <summary>Medication log. Astronauts: own crewId only. Flight surgeons: crewId required.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MedsController(
    ICrewLogService log,
    ICrewAccessService crewAccess) : ControllerBase
{
    /// <summary>Doses for one crew member, newest first; optional date (yyyy-MM-dd) filter.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MedicationLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MedicationLogDto>>> Get(
        [FromQuery] string? crewId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var result = await log.GetMedicationsAsync(access.CrewId!, date, cancellationToken);
        return this.Ok(result.Value);
    }

    /// <summary>Logs a dose (taken or pending); a taken sleep medication also marks the night on the Nutrition and Meds page.</summary>
    [HttpPost("log")]
    [ProducesResponseType<MedicationLogDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicationLogDto>> Log(
        MedicationLogCreateDto dto,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, dto.CrewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var result = await log.LogMedicationAsync(access.CrewId!, dto, cancellationToken);
        return result.Error is not null
            ? LogErrors.BadRequest(result.Field!, result.Error)
            : this.StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
