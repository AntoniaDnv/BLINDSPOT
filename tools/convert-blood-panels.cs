using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

const string CbcPath = "data/raw/LSDS-7_Complete_Blood_Count_CBC_TRANSFORMED.csv";
const string CmpPath = "data/raw/LSDS-8_Comprehensive_Metabolic_Panel_CMP_TRANSFORMED.csv";
const string OutputPath = "src/HealthMonitor.Api/Data/Files/blood-panels.json";
const string ArtifactFlag = "suspected-sample-artifact";

var specs = new AnalyteSpec[]
{
    // CBC
    new("CBC", "WBC", "10^3/µL",
        "white_blood_cell_count_value_thousand_per_microliter",
        "white_blood_cell_count_range_min_thousand_per_microliter",
        "white_blood_cell_count_range_max_thousand_per_microliter"),
    new("CBC", "Lymphocytes (absolute)", "cells/µL",
        "absolute_lymphocytes_value_cells_per_microliter",
        "absolute_lymphocytes_range_min_cells_per_microliter",
        "absolute_lymphocytes_range_max_cells_per_microliter"),
    new("CBC", "Neutrophils (absolute)", "cells/µL",
        "absolute_neutrophils_value_cells_per_microliter",
        "absolute_neutrophils_range_min_cells_per_microliter",
        "absolute_neutrophils_range_max_cells_per_microliter"),
    // The source column says "percent", but hemoglobin is reported in g/dL.
    new("CBC", "Hemoglobin", "g/dL",
        "hemoglobin_value_percent",
        "hemoglobin_range_min_percent",
        "hemoglobin_range_max_percent"),
    new("CBC", "Hematocrit", "%",
        "hematocrit_value_percent",
        "hematocrit_range_min_percent",
        "hematocrit_range_max_percent"),
    new("CBC", "Platelets", "10^3/µL",
        "platelet_count_value_thousand_per_microliter",
        "platelet_count_range_min_thousand_per_microliter",
        "platelet_count_range_max_thousand_per_microliter"),
    new("CBC", "RBC", "10^6/µL",
        "red_blood_cell_count_value_million_per_microliter",
        "red_blood_cell_count_range_min_million_per_microliter",
        "red_blood_cell_count_range_max_million_per_microliter"),
    new("CBC", "MCV", "fL",
        "mcv_value_femtoliter",
        "mcv_range_min_femtoliter",
        "mcv_range_max_femtoliter"),

    // CMP
    new("CMP", "Glucose", "mg/dL",
        "glucose_value_milligram_per_deciliter",
        "glucose_range_min_milligram_per_deciliter",
        "glucose_range_max_milligram_per_deciliter"),
    new("CMP", "Sodium", "mmol/L",
        "sodium_value_millimol_per_liter",
        "sodium_range_min_millimol_per_liter",
        "sodium_range_max_millimol_per_liter"),
    new("CMP", "Potassium", "mmol/L",
        "potassium_value_millimol_per_liter",
        "potassium_range_min_millimol_per_liter",
        "potassium_range_max_millimol_per_liter"),
    new("CMP", "Calcium", "mg/dL",
        "calcium_value_milligram_per_deciliter",
        "calcium_range_min_milligram_per_deciliter",
        "calcium_range_max_milligram_per_deciliter"),
    new("CMP", "Creatinine", "mg/dL",
        "creatinine_value_milligram_per_deciliter",
        "creatinine_range_min_milligram_per_deciliter",
        "creatinine_range_max_milligram_per_deciliter"),
    new("CMP", "Albumin", "g/dL",
        "albumin_value_gram_per_deciliter",
        "albumin_range_min_gram_per_deciliter",
        "albumin_range_max_gram_per_deciliter"),
    new("CMP", "ALP", "U/L",
        "alkaline_phosphatase_value_units_per_liter",
        "alkaline_phosphatase_range_min_units_per_liter",
        "alkaline_phosphatase_range_max_units_per_liter"),
    // ALT's max column is named differently from the usual *_range_max_* pattern.
    new("CMP", "ALT", "U/L",
        "alt_value_units_per_liter",
        "alt_range_min_units_per_liter",
        "alt_max_units_per_liter"),
};

var entries = new List<PanelEntry>();

foreach (var panelGroup in specs.GroupBy(s => s.Panel))
{
    var panel = panelGroup.Key;
    var path = panel == "CBC" ? CbcPath : CmpPath;

    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Input file not found: {path}");
        return 1;
    }

    var lines = File.ReadAllLines(path);
    if (lines.Length == 0)
    {
        Console.Error.WriteLine($"Input file is empty: {path}");
        return 1;
    }

    var header = lines[0].Split(',');
    var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    for (var i = 0; i < header.Length; i++)
    {
        columnIndex[header[i]] = i;
    }

    foreach (var spec in panelGroup)
    {
        foreach (var column in new[] { spec.ValueColumn, spec.MinColumn, spec.MaxColumn })
        {
            if (!columnIndex.ContainsKey(column))
            {
                Console.Error.WriteLine($"Column '{column}' not found in {path}");
                return 1;
            }
        }
    }

    for (var lineNumber = 1; lineNumber < lines.Length; lineNumber++)
    {
        var line = lines[lineNumber];
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var cells = line.Split(',');

        if (!columnIndex.TryGetValue("Sample Name", out var sampleNameIndex))
        {
            Console.Error.WriteLine($"Column 'Sample Name' not found in {path}");
            return 1;
        }

        var sampleName = Cell(cells, sampleNameIndex);
        if (!TryParseSampleName(sampleName, out var crewId, out var timepoint, out var dayOffset))
        {
            Console.Error.WriteLine($"Skipping unrecognized sample name: '{sampleName}'");
            continue;
        }

        // C003 / L-92 CBC is a corrupted source row; drop the whole panel for it.
        if (panel == "CBC" && crewId == "C003" && timepoint == "L-92")
        {
            continue;
        }

        foreach (var spec in panelGroup)
        {
            var valueText = Cell(cells, columnIndex[spec.ValueColumn]);
            if (IsMissing(valueText))
            {
                continue;
            }

            var value = ParseNumber(valueText, spec, sampleName);
            var rangeMin = ParseNullableNumber(Cell(cells, columnIndex[spec.MinColumn]), spec, sampleName);
            var rangeMax = ParseNullableNumber(Cell(cells, columnIndex[spec.MaxColumn]), spec, sampleName);

            string? flag = null;
            if (spec.Analyte == "Glucose" && timepoint == "R+45" && value < 65)
            {
                flag = ArtifactFlag;
            }
            else if (spec.Analyte == "Potassium" && crewId == "C004" && timepoint == "R+45")
            {
                flag = ArtifactFlag;
            }

            entries.Add(new PanelEntry(
                crewId,
                timepoint,
                dayOffset,
                panel,
                spec.Analyte,
                value,
                spec.Unit,
                rangeMin,
                rangeMax,
                flag));
        }
    }
}

var ordered = entries
    .OrderBy(e => e.CrewId, StringComparer.Ordinal)
    .ThenBy(e => e.Panel, StringComparer.Ordinal)
    .ThenBy(e => e.Analyte, StringComparer.Ordinal)
    .ThenBy(e => e.DayOffset)
    .ToList();

var outputDirectory = Path.GetDirectoryName(OutputPath);
if (!string.IsNullOrEmpty(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

File.WriteAllText(
    OutputPath,
    JsonSerializer.Serialize(ordered, PanelJsonContext.Default.ListPanelEntry) + Environment.NewLine);

var flaggedCount = ordered.Count(e => e.Flag is not null);
Console.WriteLine($"Wrote {ordered.Count} entries ({flaggedCount} flagged) to {OutputPath}");
return 0;

static string Cell(string[] cells, int index) =>
    index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;

static bool IsMissing(string text) =>
    string.IsNullOrWhiteSpace(text)
    || text.Equals("NA", StringComparison.OrdinalIgnoreCase)
    || text.Equals("N/A", StringComparison.OrdinalIgnoreCase);

static double ParseNumber(string text, AnalyteSpec spec, string sampleName)
{
    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
    {
        throw new FormatException(
            $"Invalid value '{text}' for {spec.Panel}/{spec.Analyte} in sample '{sampleName}'.");
    }

    return number;
}

static double? ParseNullableNumber(string text, AnalyteSpec spec, string sampleName) =>
    IsMissing(text) ? null : ParseNumber(text, spec, sampleName);

static bool TryParseSampleName(
    string sampleName,
    out string crewId,
    out string timepoint,
    out int dayOffset)
{
    crewId = string.Empty;
    timepoint = string.Empty;
    dayOffset = 0;

    var parts = sampleName.Split('_');
    if (parts.Length < 3 || parts[0].Length == 0)
    {
        return false;
    }

    // CBC names end in "_cbc" and CMP names do not, so locate the L-/R+ token
    // instead of relying on a fixed position.
    var timepointPart = parts.FirstOrDefault(part =>
        (part.StartsWith("L-", StringComparison.Ordinal) || part.StartsWith("R+", StringComparison.Ordinal))
        && int.TryParse(part[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

    if (timepointPart is null)
    {
        return false;
    }

    if (timepointPart.StartsWith("L-", StringComparison.Ordinal)
        && int.TryParse(timepointPart[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var daysBefore))
    {
        crewId = parts[0];
        timepoint = timepointPart;
        dayOffset = -daysBefore;
        return true;
    }

    if (timepointPart.StartsWith("R+", StringComparison.Ordinal)
        && int.TryParse(timepointPart[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var daysAfter))
    {
        crewId = parts[0];
        timepoint = timepointPart;
        dayOffset = daysAfter;
        return true;
    }

    return false;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<PanelEntry>))]
internal partial class PanelJsonContext : JsonSerializerContext
{
}

record AnalyteSpec(
    string Panel,
    string Analyte,
    string Unit,
    string ValueColumn,
    string MinColumn,
    string MaxColumn);

record PanelEntry(
    string CrewId,
    string Timepoint,
    int DayOffset,
    string Panel,
    string Analyte,
    double Value,
    string Unit,
    double? RangeMin,
    double? RangeMax,
    string? Flag);
