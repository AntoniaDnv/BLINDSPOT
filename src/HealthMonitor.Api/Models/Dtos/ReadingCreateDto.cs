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
}
