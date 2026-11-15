namespace HealthMonitor.Api.Controllers;

using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Services.Readings;
using Services.Vitals;

/// <summary>
/// Vital-sign readings. Astronauts: own crewId only. Flight surgeons: crewId required.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReadingsController(
    IReadingService readings,
    IVitalThresholdService vitals,
    ICrewAccessService crewAccess) : ControllerBase
{
    /// <summary>
    /// Several indicator values with one timestamp (Log page "Save vitals"). Each value is
    /// validated and its status computed exactly like POST /api/readings. All or nothing.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType<IReadOnlyList<ReadingBatchResultDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReadingBatchResultDto>>> CreateBatch(
        ReadingBatchCreateDto dto,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, dto.CrewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var (created, errors) = await readings.CreateBatchAsync(access.CrewId!, dto, cancellationToken);
        if (errors is not null)
        {
            return this.BadRequest(new
            {
                error = string.Join(" ", errors.Values.SelectMany(v => v)),
                errors,
            });
        }

        return this.StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost]
    [ProducesResponseType<ReadingResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReadingResponseDto>> Create(
        ReadingCreateDto dto,
        CancellationToken cancellationToken)
    {
        // dto.CrewId is only a request; the helper decides the effective crewId.
        var access = await crewAccess.ResolveAsync(this.User, dto.CrewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        if (vitals.Find(dto.Indicator) is { } threshold
            && dto.Value is { } value
            && VitalPlausibility.Validate(threshold, value) is { } invalid)
        {
            return this.BadRequest(new { error = invalid });
        }

        var created = await readings.CreateAsync(
            access.CrewId!,
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
            new { crewId = created.CrewId, indicator = created.Indicator },
            created);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReadingResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReadingResponseDto>>> GetHistory(
        [FromQuery] string? crewId,
        [FromQuery] string? indicator,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var history = await readings.GetHistoryAsync(
            access.CrewId!,
            indicator,
            cancellationToken);

        return this.Ok(history);
    }

    [HttpGet("latest")]
    [ProducesResponseType<IReadOnlyList<LatestReadingResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LatestReadingResponseDto>>> GetLatest(
        [FromQuery] string? crewId,
        CancellationToken cancellationToken)
    {
        var access = await crewAccess.ResolveAsync(this.User, crewId, cancellationToken);
        if (!access.IsAllowed)
            return access.ToErrorResult();

        var latest = await readings.GetLatestAsync(access.CrewId!, cancellationToken);

        return this.Ok(latest);
    }
}
