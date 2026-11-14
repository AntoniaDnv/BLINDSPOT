using System.Text.RegularExpressions;

namespace LabClean.Core;

/// <summary>Plausibility and unit facts for one lab test.</summary>
/// <param name="Unit">Expected unit.</param>
/// <param name="Min">Lowest physiologically possible value in that unit.</param>
/// <param name="Max">Highest physiologically possible value in that unit.</param>
public sealed record TestInfo(string Unit, double Min, double Max, params string[] Aliases);

/// <summary>An arithmetic relationship between lab values, e.g. MCHC = Hgb ÷ Hct × 100.</summary>
/// <param name="Rel">Allowed relative difference (lab rounding).</param>
/// <param name="Abs">Allowed absolute difference (lab rounding).</param>
public sealed record Identity(string Target, string[] Inputs, string Formula, Func<double[], double> Compute, double Rel, double Abs, int Decimals);

public sealed record DropRule(Regex Pattern, string Reason);

public sealed record StatusRef(string Test, string Status);

/// <summary>When many subjects show <see cref="Trigger"/> at one visit, sample handling is the likelier cause.</summary>
public sealed record PreanalyticalRule(StatusRef Trigger, StatusRef[] Companions, string Reason);

/// <summary>
/// Domain knowledge the cleaner uses. Extend these tables to support new panels;
/// <see cref="LabCleaner"/> itself has no lab-specific logic.
/// </summary>
public static class Knowledge
{
    /// <summary>Cell contents that mean "no value". Compared after trim + lowercase.</summary>
    public static readonly HashSet<string> MissingTokens = new(StringComparer.Ordinal)
    {
        "", "na", "n/a", "n.a.", "nan", "null", "none", "-", "--", ".", "nd", "n.d.", "not done", "missing", "?", "#n/a",
    };

    private static readonly Dictionary<string, string[]> UnitSpellings = new()
    {
        ["g/dL"] = ["gram_per_deciliter", "grams_per_deciliter", "g/dl", "g_dl", "gdl"],
        ["mg/dL"] = ["milligram_per_deciliter", "milligrams_per_deciliter", "mg/dl", "mg_dl"],
        ["mg/L"] = ["milligram_per_liter", "mg/l", "mg_l"],
        ["mmol/L"] = ["millimol_per_liter", "millimole_per_liter", "mmol/l", "mmol_l", "meq/l", "meq_l"],
        ["U/L"] = ["units_per_liter", "unit_per_liter", "u/l", "iu/l", "u_l"],
        ["cells/µL"] = ["cells_per_microliter", "cells/ul", "/ul", "cells_ul", "cells/mm3"],
        ["×10³/µL"] = ["thousand_per_microliter", "10^3/ul", "x10^3/ul", "k/ul", "10e3/ul", "x10e3/ul", "10^9/l", "x10^9/l"],
        ["×10⁶/µL"] = ["million_per_microliter", "10^6/ul", "x10^6/ul", "m/ul", "10e6/ul", "x10e6/ul", "10^12/l", "x10^12/l"],
        ["%"] = ["percent", "%", "pct", "percentage"],
        ["pg"] = ["picogram", "picograms", "pg"],
        ["fL"] = ["femtoliter", "femtoliters", "fl"],
        ["mL/min/1.73m²"] = ["milliliter_per_minute_per_1.73_meter_squared", "ml/min/1.73m2", "ml/min/1.73_m2"],
        ["ratio"] = ["", "ratio"],
    };

    public static string NormUnitKey(string? s) =>
        Regex.Replace((s ?? "").Trim().ToLowerInvariant().Replace('µ', 'u').Replace('μ', 'u'), @"\s+", "");

    /// <summary>Raw unit spelling (normalised with <see cref="NormUnitKey"/>) → canonical unit.</summary>
    public static readonly Dictionary<string, string> UnitAliases = UnitSpellings
        .SelectMany(kv => kv.Value.Select(s => (Key: NormUnitKey(s), Canon: kv.Key)))
        .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Canon);

    private static TestInfo Pct(params string[] aliases) => new("%", 0, 100, aliases);
    private static TestInfo AbsCount(params string[] aliases) => new("cells/µL", 0, 100000, aliases);

    public static readonly Dictionary<string, TestInfo> Tests = new()
    {
        // Complete blood count
        ["white_blood_cell_count"] = new("×10³/µL", 0.3, 200, "wbc", "white_blood_cells", "leukocytes"),
        ["red_blood_cell_count"] = new("×10⁶/µL", 1, 9, "rbc", "red_blood_cells", "erythrocytes"),
        ["hemoglobin"] = new("g/dL", 3, 25, "hgb", "hb", "haemoglobin"),
        ["hematocrit"] = new("%", 10, 75, "hct", "haematocrit"),
        ["mcv"] = new("fL", 50, 140, "mean_corpuscular_volume"),
        ["mch"] = new("pg", 12, 50, "mean_corpuscular_hemoglobin"),
        ["mchc"] = new("g/dL", 22, 40, "mean_corpuscular_hemoglobin_concentration"),
        ["rdw"] = new("%", 8, 35, "red_cell_distribution_width"),
        ["platelet_count"] = new("×10³/µL", 5, 2000, "plt", "platelets"),
        ["mpv"] = new("fL", 5, 16, "mean_platelet_volume"),
        ["neutrophils"] = Pct("neut", "neutrophils_percent"),
        ["lymphocytes"] = Pct("lymph", "lymphocytes_percent"),
        ["monocytes"] = Pct("mono", "monocytes_percent"),
        ["eosinophils"] = Pct("eos", "eosinophils_percent"),
        ["basophils"] = Pct("baso", "basophils_percent"),
        ["absolute_neutrophils"] = AbsCount("anc", "neut_abs", "neutrophils_absolute"),
        ["absolute_lymphocytes"] = AbsCount("alc", "lymph_abs", "lymphocytes_absolute"),
        ["absolute_monocytes"] = AbsCount("mono_abs", "monocytes_absolute"),
        ["absolute_eosinophils"] = AbsCount("eos_abs", "eosinophils_absolute"),
        ["absolute_basophils"] = AbsCount("baso_abs", "basophils_absolute"),
        // Comprehensive metabolic panel
        ["glucose"] = new("mg/dL", 15, 1500, "glu", "blood_glucose"),
        ["sodium"] = new("mmol/L", 100, 190, "na"),
        ["potassium"] = new("mmol/L", 1.5, 10, "k"),
        ["chloride"] = new("mmol/L", 60, 150, "cl"),
        ["carbon_dioxide"] = new("mmol/L", 3, 50, "co2", "bicarbonate", "hco3", "total_co2"),
        ["urea_nitrogen_bun"] = new("mg/dL", 1, 250, "bun", "urea_nitrogen", "blood_urea_nitrogen"),
        ["creatinine"] = new("mg/dL", 0.1, 25, "creat", "cr"),
        ["calcium"] = new("mg/dL", 3, 18, "ca"),
        ["albumin"] = new("g/dL", 0.5, 7, "alb"),
        ["total_protein"] = new("g/dL", 2, 13, "tp", "protein_total"),
        ["globulin"] = new("g/dL", 0.3, 9, "glob"),
        ["albumin_to_globulin_ratio"] = new("ratio", 0.1, 6, "a_g_ratio", "ag_ratio", "a/g_ratio"),
        ["bun_to_creatinine_ratio"] = new("ratio", 1, 100, "bun_creatinine_ratio", "bun/creatinine_ratio"),
        ["alkaline_phosphatase"] = new("U/L", 5, 3000, "alp", "alk_phos"),
        ["alt"] = new("U/L", 1, 10000, "alanine_aminotransferase", "sgpt"),
        ["ast"] = new("U/L", 1, 10000, "aspartate_aminotransferase", "sgot"),
        ["total_bilirubin"] = new("mg/dL", 0.05, 50, "bilirubin", "tbil", "bilirubin_total"),
        ["egfr"] = new("mL/min/1.73m²", 1, 250, "estimated_gfr"),
    };

    /// <summary>Normalised test name or alias → canonical test name.</summary>
    public static readonly Dictionary<string, string> TestAliases = Tests
        .SelectMany(kv => kv.Value.Aliases.Select(a => (Key: Parsing.NormTest(a), Name: kv.Key)).Prepend((Key: kv.Key, Name: kv.Key)))
        .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Name);

    /// <summary>Columns removed on purpose, with the reason written to the report.</summary>
    public static readonly DropRule[] DropRules =
    [
        new(new Regex("^egfr_.*(african|black|race)", RegexOptions.Compiled),
            "Race-based eGFR equations are no longer recommended (CKD-EPI 2021)."),
    ];

    /// <summary>
    /// Identities between lab values: used to fill blanks, detect bad cells and repair them.
    /// The absolute tolerance on the differential covers the % being rounded to one decimal.
    /// </summary>
    public static readonly Identity[] Identities =
    [
        .. new[] { "neutrophils", "lymphocytes", "monocytes", "eosinophils", "basophils" }.Select(cell =>
            new Identity($"absolute_{cell}", ["white_blood_cell_count", cell], "WBC × % × 10", v => v[0] * 10 * v[1], 0.15, 15, 0)),
        new("mchc", ["hemoglobin", "hematocrit"], "Hgb ÷ Hct × 100", v => v[0] / v[1] * 100, 0.04, 0, 1),
        new("mch", ["hemoglobin", "red_blood_cell_count"], "Hgb ÷ RBC × 10", v => v[0] / v[1] * 10, 0.04, 0, 1),
        new("mcv", ["hematocrit", "red_blood_cell_count"], "Hct ÷ RBC × 10", v => v[0] / v[1] * 10, 0.04, 0, 1),
        new("globulin", ["total_protein", "albumin"], "Total protein − albumin", v => v[0] - v[1], 0, 0.25, 1),
        new("albumin_to_globulin_ratio", ["albumin", "globulin"], "Albumin ÷ globulin", v => v[0] / v[1], 0.12, 0, 1),
        new("bun_to_creatinine_ratio", ["urea_nitrogen_bun", "creatinine"], "BUN ÷ creatinine", v => v[0] / v[1], 0.12, 0, 1),
    ];

    /// <summary>Delayed processing lets cells consume glucose and leak potassium.</summary>
    public static readonly PreanalyticalRule[] PreanalyticalRules =
    [
        new(new("glucose", "low"), [new("potassium", "high")],
            "Low glucose in several subjects at one visit (often with high potassium) usually means delayed sample processing."),
    ];
}
