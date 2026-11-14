namespace HealthMonitor.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.BloodPanels;

[ApiController]
[Route("api/[controller]")]
public class BloodPanelsController(
    IBloodPanelService bloodPanels) : ControllerBase
{
    [HttpGet("crew")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCrewIds(
        CancellationToken cancellationToken)
    {
        var crewIds = await bloodPanels.GetCrewIdsAsync(cancellationToken);
        return this.Ok(crewIds);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BloodPanelResponseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BloodPanelResponseDto>>> GetByCrew(
        [FromQuery] string crewId,
        [FromQuery] string? panel = null,
        CancellationToken cancellationToken = default)
    {
        var results = await bloodPanels.GetByCrewAsync(
            crewId,
            panel,
            cancellationToken);

        return this.Ok(results);
    }
}
