namespace HealthMonitor.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Readings;

[ApiController]
[Route("api/[controller]")]
public class ReadingsController(
    IReadingService readings) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ReadingResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReadingResponseDto>> Create(
        ReadingCreateDto dto,
        CancellationToken cancellationToken)
    {
        var created = await readings.CreateAsync(
            dto,
            cancellationToken);

        if (created is null)
        {
            return this.BadRequest(new
            {
                error = $"Unknown indicator '{dto.Indicator}'.",
            });
        }

        return this.CreatedAtAction(
            nameof(GetHistory),
            new { indicator = created.Indicator },
            created);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReadingResponseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReadingResponseDto>>> GetHistory(
        [FromQuery] string? indicator,
        CancellationToken cancellationToken)
    {
        var history = await readings.GetHistoryAsync(
            indicator,
            cancellationToken);

        return this.Ok(history);
    }

    [HttpGet("latest")]
    [ProducesResponseType<IReadOnlyList<LatestReadingResponseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LatestReadingResponseDto>>> GetLatest(
        CancellationToken cancellationToken)
    {
        var latest = await readings.GetLatestAsync(cancellationToken);

        return this.Ok(latest);
    }
}
