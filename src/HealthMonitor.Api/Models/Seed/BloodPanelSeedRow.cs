namespace HealthMonitor.Api.Models.Seed;

public class BloodPanelSeedRow
{
    public string CrewId { get; init; } = "";

    public string Timepoint { get; init; } = "";

    public int DayOffset { get; init; }

    public string Panel { get; init; } = "";

    public string Analyte { get; init; } = "";

    public double Value { get; init; }

    public string Unit { get; init; } = "";

    public double? RangeMin { get; init; }

    public double? RangeMax { get; init; }

    public string? Flag { get; init; }
}
