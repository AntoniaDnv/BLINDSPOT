namespace HealthMonitor.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models.Vitals;
using Services.Vitals;

[ApiController]
[Route("api/[controller]")]
public class VitalsController(
    IVitalThresholdService vitals) : ControllerBase
{
    [HttpGet("indicators")]
    [ProducesResponseType<IReadOnlyList<VitalThreshold>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<VitalThreshold>> GetIndicators()
        => this.Ok(vitals.GetAll());
}
