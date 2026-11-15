namespace HealthMonitor.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Radiation;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RadiationController(IRadiationService radiation) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<RadiationStatusResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RadiationStatusResponseDto>> GetStatus(
        [FromQuery] int? days = null,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        CancellationToken cancellationToken = default)
    {
        // Determine date range from params
        DateTime? startDate = null;
        DateTime? endDate = end;

        if (days.HasValue && days.Value > 0)
        {
            endDate = DateTime.UtcNow;
            startDate = endDate.Value.AddDays(-days.Value);
        }
        else if (start.HasValue)
        {
            startDate = start;
        }

        // Validate: start must not be after end
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return this.BadRequest(new
            {
                error = "Start date cannot be after end date.",
            });
        }

        var status = await radiation.GetStatusAsync(startDate, endDate, cancellationToken);

        return this.Ok(status);
    }
}
