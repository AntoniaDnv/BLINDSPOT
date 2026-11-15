namespace HealthMonitor.Api.Models.Dtos;

public class ReadingResponseDto
{
    public int Id { get; init; }

    public string CrewId { get; init; } = "";

    public string Indicator { get; init; } = "";

    public double Value { get; init; }

    public DateTime RecordedAt { get; init; }

    public string Status { get; init; } = "";
}
