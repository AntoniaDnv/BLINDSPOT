namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Logging;

/// <summary>Subjective symptoms. Astronauts: own crewId only. Flight surgeons: crewId required.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SymptomsController(
    ICrewLogService log,
    ICrewAccessService crewAccess) : ControllerBase
{
    /// <summary>Newest first (default 50, max 200).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SymptomResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SymptomResponseDto>>> Get(
        [FromQuery] string? crewId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        return this.Ok(await log.GetSymptomsAsync(access.CrewId!, limit, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<SymptomResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SymptomResponseDto>> Create(
        SymptomCreateDto dto,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, dto.CrewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var created = await log.LogSymptomAsync(access.CrewId!, dto, cancellationToken);
        return this.StatusCode(StatusCodes.Status201Created, created);
    }
}
