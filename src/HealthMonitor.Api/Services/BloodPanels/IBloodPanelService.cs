namespace HealthMonitor.Api.Services.BloodPanels;

using Models.Dtos;

public interface IBloodPanelService
{
    Task<IReadOnlyList<string>> GetCrewIdsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BloodPanelResponseDto>> GetByCrewAsync(
        string crewId,
        string? panel = null,
        CancellationToken cancellationToken = default);
}
