namespace HealthMonitor.Api.Models.Dtos;

using System.ComponentModel.DataAnnotations;

// ---- Vitals batch -------------------------------------------------------------

public class ReadingBatchItemDto
{
    [Required(ErrorMessage = "Indicator is required.")]
    [StringLength(50)]
    public string Indicator { get; init; } = "";

    [Required(ErrorMessage = "Value is required.")]
    public double? Value { get; init; }
}

public class ReadingBatchCreateDto
{
    /// <summary>Flight surgeons must send it; astronauts may omit it (own crewId).</summary>
    [StringLength(20)]
    public string? CrewId { get; init; }

    /// <summary>One timestamp for every value (default: now).</summary>
    public DateTime? RecordedAt { get; init; }

    [Required(ErrorMessage = "Enter at least one value.")]
    [MinLength(1, ErrorMessage = "Enter at least one value.")]
    [MaxLength(20)]
    public List<ReadingBatchItemDto> Values { get; init; } = [];
}

public class ReadingBatchResultDto
{
    public int Id { get; init; }

    public string CrewId { get; init; } = "";

    public string Indicator { get; init; } = "";

    public string Name { get; init; } = "";

    public string Unit { get; init; } = "";

    public double Value { get; init; }

    public DateTime RecordedAt { get; init; }

    public string Status { get; init; } = "";

    /// <summary>The guidance text that matches <see cref="Status"/>.</summary>
    public string Guidance { get; init; } = "";

    public string NasaRisk { get; init; } = "";
}

// ---- Nutrition ----------------------------------------------------------------

public class NutritionLogCreateDto
{
    [StringLength(20)]
    public string? CrewId { get; init; }

    /// <summary>Calendar date (yyyy-MM-dd) of the mission day; default today (UTC).</summary>
    public DateOnly? Date { get; init; }

    [Range(1, 5000, ErrorMessage = "Water must be between 1 and 5000 ml.")]
    public double? WaterMl { get; init; }

    [Range(1, 10000, ErrorMessage = "Calories must be between 1 and 10000 kcal.")]
    public double? Kcal { get; init; }

    [Range(0, 1000, ErrorMessage = "Protein must be between 0 and 1000 g.")]
    public double? ProteinG { get; init; }

    [Range(0, 2000, ErrorMessage = "Carbohydrates must be between 0 and 2000 g.")]
    public double? CarbsG { get; init; }

    [Range(0, 1000, ErrorMessage = "Fat must be between 0 and 1000 g.")]
    public double? FatG { get; init; }

    [StringLength(100, ErrorMessage = "Meal name must be at most 100 characters.")]
    public string? Meal { get; init; }
}

public class NutritionLogEntryDto
{
    public int Id { get; init; }

    public DateTime LoggedAt { get; init; }

    public double? WaterMl { get; init; }

    public double? Kcal { get; init; }

    public double? ProteinG { get; init; }

    public double? CarbsG { get; init; }

    public double? FatG { get; init; }

    public string? Meal { get; init; }
}

public class MedicationLogDto
{
    public int Id { get; init; }

    public string CrewId { get; init; } = "";

    public int Day { get; init; }

    public DateOnly Date { get; init; }

    public string Medication { get; init; } = "";

    public string Dose { get; init; } = "";

    public bool Taken { get; init; }

    public string? Time { get; init; }

    public bool IsSleepMedication { get; init; }

    public DateTime LoggedAt { get; init; }
}

/// <summary>One mission day in GET /api/nutrition (file fields + logged additions).</summary>
public class NutritionDayDto
{
    public string CrewId { get; init; } = "";

    public int Day { get; init; }

    public DateOnly Date { get; init; }

    public double Kcal { get; init; }

    public double WaterL { get; init; }

    public double? ProteinG { get; init; }

    public double? CarbsG { get; init; }

    public double? FatG { get; init; }

    public bool HypnoticTaken { get; init; }

    public IReadOnlyList<string> Meals { get; init; } = [];

    public IReadOnlyList<NutritionLogEntryDto> Entries { get; init; } = [];

    public IReadOnlyList<MedicationLogDto> Meds { get; init; } = [];
}

/// <summary>Today's mission day (team demo parameter: today = the day after the simulated file ends).</summary>
public class MissionTodayDto
{
    public int Day { get; init; }

    public DateOnly Date { get; init; }
}

// ---- Medications --------------------------------------------------------------

public class MedicationLogCreateDto
{
    [StringLength(20)]
    public string? CrewId { get; init; }

    public DateOnly? Date { get; init; }

    [Required(ErrorMessage = "Medication is required.")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "Medication must be 1-80 characters.")]
    public string Medication { get; init; } = "";

    [Required(ErrorMessage = "Dose is required.")]
    [StringLength(40, MinimumLength = 1, ErrorMessage = "Dose must be 1-40 characters.")]
    public string Dose { get; init; } = "";

    [Required(ErrorMessage = "Taken is required.")]
    public bool? Taken { get; init; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Time must be HH:mm (24-hour).")]
    public string? Time { get; init; }

    public bool IsSleepMedication { get; init; }
}

// ---- Symptoms -----------------------------------------------------------------

public class SymptomCreateDto
{
    [StringLength(20)]
    public string? CrewId { get; init; }

    public DateTime? RecordedAt { get; init; }

    [Required(ErrorMessage = "Fatigue is required.")]
    [Range(0, 10, ErrorMessage = "Fatigue must be between 0 and 10.")]
    public int? Fatigue { get; init; }

    [Required(ErrorMessage = "Headache is required.")]
    [Range(0, 10, ErrorMessage = "Headache must be between 0 and 10.")]
    public int? Headache { get; init; }

    public bool Congestion { get; init; }

    public bool VisionChange { get; init; }

    [StringLength(1000, ErrorMessage = "Notes must be at most 1000 characters.")]
    public string? Notes { get; init; }
}

public class SymptomResponseDto
{
    public int Id { get; init; }

    public string CrewId { get; init; } = "";

    public DateTime RecordedAt { get; init; }

    public int Fatigue { get; init; }

    public int Headache { get; init; }

    public bool Congestion { get; init; }

    public bool VisionChange { get; init; }

    public string Notes { get; init; } = "";
}

// ---- Recent entries -----------------------------------------------------------

public class RecentEntryDto
{
    /// <summary>vitals | nutrition | symptoms | meds</summary>
    public string Type { get; init; } = "";

    public DateTime RecordedAt { get; init; }

    public string Summary { get; init; } = "";

    /// <summary>ok | warning | critical | taken | pending, or null.</summary>
    public string? Status { get; init; }

    /// <summary>True for seeded simulated demo data (not something the user logged).</summary>
    public bool Simulated { get; init; }
}
