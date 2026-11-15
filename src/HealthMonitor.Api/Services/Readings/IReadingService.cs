namespace HealthMonitor.Api.Services.Readings;

using Models.Dtos;

public interface IReadingService
{
    /// <param name="crewId">Already authorised by ICrewAccessService; dto.CrewId is ignored.</param>
    Task<ReadingResponseDto?> CreateAsync(
        string crewId,
        ReadingCreateDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates every value first (unknown indicator, duplicates, implausible values);
    /// on any error nothing is saved and the errors are returned keyed by indicator.
    /// </summary>
    Task<(IReadOnlyList<ReadingBatchResultDto>? Created, Dictionary<string, string[]>? Errors)> CreateBatchAsync(
        string crewId,
        ReadingBatchCreateDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingResponseDto>> GetHistoryAsync(
        string crewId,
        string? indicator,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LatestReadingResponseDto>> GetLatestAsync(
        string crewId,
        CancellationToken cancellationToken = default);
}
