using System.Text.Json.Serialization;

namespace LabClean.Core;

public sealed class CleanOptions
{
    /// <summary>Column holding the sample ID. Auto-detected when null.</summary>
    public string? IdColumn { get; init; }
    /// <summary>Column holding the subject ID. Read from the sample ID when null.</summary>
    public string? SubjectColumn { get; init; }
    /// <summary>Column holding the visit/timepoint. Read from the sample ID when null.</summary>
    public string? VisitColumn { get; init; }
    /// <summary>Regex for the subject inside the sample ID; may use a group named "subject".</summary>
    public string? SubjectRegex { get; init; }
    /// <summary>Move values that sit in the wrong column back where they fit.</summary>
    public bool Repair { get; init; } = true;
}

public sealed class Issue
{
    /// <summary>"error", "warning" or "info".</summary>
    public required string Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Count { get; init; }
    public List<string> Examples { get; init; } = [];
}

public sealed record Change(string Sample, string Test, double? From, double To, string Reason);

/// <summary>One sample × test, in tidy long format.</summary>
public sealed class LabRecord
{
    public required string Subject { get; init; }
    public required string SampleId { get; init; }
    public string? Visit { get; init; }
    public string? Phase { get; init; }
    public int? Day { get; init; }
    public required string Test { get; init; }
    public double? Value { get; init; }
    public required string Unit { get; init; }
    public double? RefMin { get; init; }
    public double? RefMax { get; init; }
    public bool RefMinOpen { get; init; }
    public bool RefMaxOpen { get; init; }
    /// <summary>"normal", "low", "high", "no_range" or null when there is no value.</summary>
    public string? Status { get; init; }
    public List<string> Flags { get; init; } = [];
    /// <summary>Holds "originalValue" and "notes" only when the cleaner changed or annotated the value.</summary>
    [JsonExtensionData] public Dictionary<string, object?>? Extra { get; set; }

    [JsonIgnore] public bool Changed => Extra?.ContainsKey("originalValue") == true;
    [JsonIgnore] public double? OriginalValue => Extra?.GetValueOrDefault("originalValue") as double?;
}

public sealed class Baseline
{
    public required string Unit { get; init; }
    public List<double> Values { get; init; } = [];
    public List<string?> Visits { get; init; } = [];
    public int N { get; set; }
    public double Mean { get; set; }
    public double? Sd { get; set; }
}

public sealed class BaselineSet
{
    public string? Method { get; init; }
    /// <summary>subject → test → baseline.</summary>
    public Dictionary<string, Dictionary<string, Baseline>> Baselines { get; init; } = [];
}

public sealed class IssueCounts
{
    public int Error { get; init; }
    public int Warning { get; init; }
    public int Info { get; init; }
}

public sealed class Summary
{
    public int Rows { get; init; }
    public int Samples { get; init; }
    public int Subjects { get; init; }
    public int Visits { get; init; }
    public int Tests { get; init; }
    public int Cells { get; init; }
    public int Missing { get; init; }
    public int Changed { get; init; }
    public int Implausible { get; init; }
    public int Outliers { get; init; }
    public int Artifacts { get; init; }
    public double CleanCellsPercent { get; init; }
    public required IssueCounts Issues { get; init; }
}

public sealed class ColumnInfo
{
    public required Dictionary<string, string> Columns { get; init; }
    public required string Unit { get; init; }
    public required string UnitAsWritten { get; init; }
    public bool Known { get; init; }
}

public sealed class CleanResult
{
    public required Summary Summary { get; init; }
    public required List<Issue> Issues { get; init; }
    public required List<Change> Changes { get; init; }
    public required List<LabRecord> Records { get; init; }
    public required BaselineSet Baselines { get; init; }
    public required Dictionary<string, ColumnInfo> Schema { get; init; }
    public required string IdColumn { get; init; }
}
