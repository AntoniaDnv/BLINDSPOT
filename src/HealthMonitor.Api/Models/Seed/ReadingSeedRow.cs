namespace HealthMonitor.Api.Models.Seed;

public class ReadingSeedRow
{
    public string Indicator { get; init; } = "";

    public double Value { get; init; }

    public DateTime RecordedAt { get; init; }
}
