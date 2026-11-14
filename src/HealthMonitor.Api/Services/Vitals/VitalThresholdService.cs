namespace HealthMonitor.Api.Services.Vitals;

using System.Text.Json;
using Data.Seeding;
using Models.Vitals;

public class VitalThresholdService : IVitalThresholdService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyList<VitalThreshold> thresholds;
    private readonly Dictionary<string, VitalThreshold> byIndicator;

    public VitalThresholdService()
    {
        var path = SeedFilePaths.Resolve("vital-thresholds.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Vital thresholds file not found: {path}",
                path);
        }

        using var stream = File.OpenRead(path);
        var loaded = JsonSerializer.Deserialize<List<VitalThreshold>>(
            stream,
            Options) ?? [];

        thresholds = loaded.AsReadOnly();
        byIndicator = loaded.ToDictionary(
            t => t.Indicator,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<VitalThreshold> GetAll()
        => thresholds;

    public VitalThreshold? Find(string? indicator)
        => !string.IsNullOrWhiteSpace(indicator)
            && byIndicator.TryGetValue(indicator, out var threshold)
                ? threshold
                : null;

    public string GetStatus(
        VitalThreshold threshold,
        double value)
    {
        if (value >= threshold.OkMin && value <= threshold.OkMax)
            return "ok";

        if (value >= threshold.WarnMin && value <= threshold.WarnMax)
            return "warning";

        return "critical";
    }
}
