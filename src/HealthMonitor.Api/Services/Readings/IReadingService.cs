namespace HealthMonitor.Api.Services.Readings;

using Models.Dtos;

public interface IReadingService
{
    /// <param name="crewId">Already authorised by ICrewAccessService; dto.CrewId is ignored.</param>
    Task<ReadingResponseDto?> CreateAsync(
        string crewId,
        ReadingCreateDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingResponseDto>> GetHistoryAsync(
        string crewId,
        string? indicator,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LatestReadingResponseDto>> GetLatestAsync(
        string crewId,
        CancellationToken cancellationToken = default);
}
