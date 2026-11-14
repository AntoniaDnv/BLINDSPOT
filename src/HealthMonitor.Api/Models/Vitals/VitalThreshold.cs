namespace HealthMonitor.Api.Models.Vitals;

public class VitalThreshold
{
    public string Indicator { get; init; } = "";

    public string Name { get; init; } = "";

    public string Unit { get; init; } = "";

    public double OkMin { get; init; }

    public double OkMax { get; init; }

    public double WarnMin { get; init; }

    public double WarnMax { get; init; }

    public string NasaRisk { get; init; } = "";

    public VitalGuidance Guidance { get; init; } = new();

    public string Source { get; init; } = "";
}
