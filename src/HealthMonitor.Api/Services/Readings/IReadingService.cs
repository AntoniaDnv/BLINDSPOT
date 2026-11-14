namespace HealthMonitor.Api.Services.Readings;

using Models.Dtos;

public interface IReadingService
{
    Task<ReadingResponseDto?> CreateAsync(
        ReadingCreateDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingResponseDto>> GetHistoryAsync(
        string? indicator,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LatestReadingResponseDto>> GetLatestAsync(
        CancellationToken cancellationToken = default);
}
