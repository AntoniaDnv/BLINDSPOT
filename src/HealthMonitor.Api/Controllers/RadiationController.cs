namespace HealthMonitor.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Radiation;

[ApiController]
[Route("api/[controller]")]
public class RadiationController(IRadiationService radiation) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<RadiationStatusResponseDto>(StatusCodes.Status200OK)]
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
            return this.StatusCode(502, new
            {
                error = "Start date cannot be after end date.",
            });
        }

        var status = await radiation.GetStatusAsync(startDate, endDate, cancellationToken);

        return this.Ok(status);
    }
}
