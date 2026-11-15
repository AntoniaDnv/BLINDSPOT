namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Logging;
using Services.Nutrition;

/// <summary>
/// Nutrition / hydration / meds (simulated file data + logged entries).
/// Astronauts: own crewId only. Flight surgeons: crewId required.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NutritionController(
    ICrewLogService log,
    ICrewAccessService crewAccess) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NutritionCrewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NutritionCrewDto>> Get(
        [FromQuery] string? crewId,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        return this.Ok(await log.GetNutritionAsync(access.CrewId!, cancellationToken));
    }

    /// <summary>Adds water / calories / macros / a meal to the crew member's day (creates the day if missing).</summary>
    [HttpPost("log")]
    [ProducesResponseType<NutritionDayDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NutritionDayDto>> Log(
        NutritionLogCreateDto dto,
        CancellationToken cancellationToken)
    {
        // dto.CrewId is only a request; the helper decides the effective crewId.
        var access = await crewAccess.ResolveAsync(this.User, dto.CrewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var result = await log.LogNutritionAsync(access.CrewId!, dto, cancellationToken);
        return result.Error is not null
            ? LogErrors.BadRequest(result.Field!, result.Error)
            : this.StatusCode(StatusCodes.Status201Created, result.Value);
    }
}

/// <summary>400 body shared by the log endpoints: { error, errors: { field: [message] } }.</summary>
internal static class LogErrors
{
    public static BadRequestObjectResult BadRequest(string field, string message)
        => new(new { error = message, errors = new Dictionary<string, string[]> { [field] = [message] } });
}
