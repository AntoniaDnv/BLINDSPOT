namespace HealthMonitor.Api.Models.Dtos;

public class BloodPanelResponseDto
{
    public string CrewId { get; init; } = "";

    public string Panel { get; init; } = "";

    public string Analyte { get; init; } = "";

    public string Timepoint { get; init; } = "";

    public int DayOffset { get; init; }

    public double Value { get; init; }

    public string? Unit { get; init; }

    public double? RangeMin { get; init; }

    public double? RangeMax { get; init; }

    public string Status { get; init; } = "";

    public string? Flag { get; init; }
}
