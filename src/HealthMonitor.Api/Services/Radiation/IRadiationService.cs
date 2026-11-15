namespace HealthMonitor.Api.Services.Radiation;

using Models.Dtos;

public interface IRadiationService
{
    Task<RadiationStatusResponseDto> GetStatusAsync(
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken cancellationToken = default);
}
