namespace HealthMonitor.Api.Models.Entities;

public class Reading
{
    public int Id { get; init; }

    public string Indicator { get; set; } = "";

    public double Value { get; set; }

    public DateTime RecordedAt { get; set; }
}
