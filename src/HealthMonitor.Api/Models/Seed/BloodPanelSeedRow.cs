namespace HealthMonitor.Api.Models.Seed;

public class BloodPanelSeedRow
{
    public string CrewId { get; set; } = "";

    public string Timepoint { get; set; } = "";

    public int DayOffset { get; set; }

    public string Panel { get; set; } = "";

    public string Analyte { get; set; } = "";

    public double Value { get; set; }

    public string? Unit { get; set; }

    public double? RangeMin { get; set; }

    public double? RangeMax { get; set; }

    public string? Flag { get; set; }
}
