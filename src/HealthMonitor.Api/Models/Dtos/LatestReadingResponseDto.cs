namespace HealthMonitor.Api.Models.Dtos;

using Models.Vitals;

public class LatestReadingResponseDto
{
    public int Id { get; init; }

    public string CrewId { get; init; } = "";

    public string Indicator { get; init; } = "";

    public string Name { get; init; } = "";

    public string Unit { get; init; } = "";

    public double Value { get; init; }

    public DateTime RecordedAt { get; init; }

    public string Status { get; init; } = "";

    public string NasaRisk { get; init; } = "";

    /// <summary>True when the latest reading is seeded simulated data.</summary>
    public bool IsSimulated { get; init; }

    public VitalGuidance Guidance { get; init; } = new();
}
