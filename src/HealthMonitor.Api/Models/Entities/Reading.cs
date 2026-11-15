namespace HealthMonitor.Api.Models.Entities;

public class Reading
{
    public int Id { get; init; }

    /// <summary>Crew member the reading belongs to (seeded simulated readings: C001).</summary>
    public string CrewId { get; set; } = "";

    public string Indicator { get; set; } = "";

    public double Value { get; set; }

    public DateTime RecordedAt { get; set; }
}
