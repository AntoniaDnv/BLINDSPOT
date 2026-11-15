namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Nutrition;

/// <summary>
/// Simulated nutrition / hydration / meds. Astronauts: own crewId only.
/// Flight surgeons: crewId required.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NutritionController(
    INutritionService nutrition,
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

        return this.Ok(nutrition.GetForCrew(access.CrewId!));
    }
}
