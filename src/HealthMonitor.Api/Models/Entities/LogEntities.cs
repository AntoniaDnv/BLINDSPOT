namespace HealthMonitor.Api.Models.Entities;

/// <summary>
/// One crew member's nutrition totals for one mission day. Seeded from
/// Data/Files/nutrition-days.json; POST /api/nutrition/log and /api/meds/log add to it.
/// </summary>
public class NutritionDay
{
    public int Id { get; init; }

    public string CrewId { get; set; } = "";

    /// <summary>Mission day (1-based), the same numbering as the data file.</summary>
    public int Day { get; set; }

    public double Kcal { get; set; }

    public double WaterL { get; set; }

    /// <summary>Macros are only known for logged entries (the file has none).</summary>
    public double? ProteinG { get; set; }

    public double? CarbsG { get; set; }

    public double? FatG { get; set; }

    /// <summary>Sleep medication (hypnotic) taken that night (file field).</summary>
    public bool HypnoticTaken { get; set; }
}

/// <summary>One POST /api/nutrition/log (water, food, a named meal), kept for "recent entries".</summary>
public class NutritionLogEntry
{
    public int Id { get; init; }

    public string CrewId { get; set; } = "";

    public int Day { get; set; }

    public DateTime LoggedAt { get; set; }

    public double? WaterMl { get; set; }

    public double? Kcal { get; set; }

    public double? ProteinG { get; set; }

    public double? CarbsG { get; set; }

    public double? FatG { get; set; }

    public string? Meal { get; set; }
}

/// <summary>A logged medication dose (taken or still pending) for one mission day.</summary>
public class MedicationLog
{
    public int Id { get; init; }

    public string CrewId { get; set; } = "";

    public int Day { get; set; }

    public string Medication { get; set; } = "";

    public string Dose { get; set; } = "";

    public bool Taken { get; set; }

    /// <summary>Planned / taken time of day, "HH:mm" (optional).</summary>
    public string? Time { get; set; }

    /// <summary>Counts as the night's sleep medication (sets NutritionDay.HypnoticTaken when taken).</summary>
    public bool IsSleepMedication { get; set; }

    public DateTime LoggedAt { get; set; }
}

/// <summary>Subjective symptoms reported by a crew member.</summary>
public class SymptomLog
{
    public int Id { get; init; }

    public string CrewId { get; set; } = "";

    public DateTime RecordedAt { get; set; }

    /// <summary>0 (none) - 10 (worst).</summary>
    public int Fatigue { get; set; }

    /// <summary>0 (none) - 10 (worst).</summary>
    public int Headache { get; set; }

    public bool Congestion { get; set; }

    public bool VisionChange { get; set; }

    /// <summary>Free text, at most 1000 characters. Rendered as text only, never as HTML.</summary>
    public string Notes { get; set; } = "";
}
