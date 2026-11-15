namespace HealthMonitor.Api.Models.Dtos;

using System.ComponentModel.DataAnnotations;

public class ReadingCreateDto
{
    [Required]
    [StringLength(50)]
    public string Indicator { get; init; } = "";

    [Required]
    public double? Value { get; init; }

    public DateTime? RecordedAt { get; init; }

    /// <summary>
    /// Crew member to log for. Flight surgeons must send it; astronauts may omit it
    /// (their own crewId is used) and get 403 if they send someone else's.
    /// </summary>
    [StringLength(20)]
    public string? CrewId { get; init; }
}
