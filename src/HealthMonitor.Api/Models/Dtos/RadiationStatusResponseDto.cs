namespace HealthMonitor.Api.Models.Dtos;

public class RadiationStatusResponseDto
{
    public string Status { get; init; } = "";

    public string Guidance { get; init; } = "";

    public int EventCount { get; init; }

    public string? LastEventTime { get; init; }

    public int FlareCount { get; init; }

    public int CmeCount { get; init; }

    public string? Error { get; init; }
}
