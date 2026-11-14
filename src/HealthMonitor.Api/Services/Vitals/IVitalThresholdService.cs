namespace HealthMonitor.Api.Services.Vitals;

using Models.Vitals;

public interface IVitalThresholdService
{
    IReadOnlyList<VitalThreshold> GetAll();

    VitalThreshold? Find(string? indicator);

    string GetStatus(
        VitalThreshold threshold,
        double value);
}
