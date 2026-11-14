using LabClean.Core;

namespace LabClean.Tests;

public class LabCleanerTests
{
    // Walks up from the test binary to the repo root (the folder that holds data/raw).
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "raw"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not find repo root containing data/raw");
    }

    private static CleanResult Load(params string[] relative) =>
        LabCleaner.Run(File.ReadAllText(Path.Combine([RepoRoot, .. relative])));

    private static LabRecord Find(CleanResult r, string sampleId, string test) =>
        r.Records.Single(x => x.SampleId == sampleId && x.Test == test);

    private static List<string> Codes(CleanResult r) => r.Issues.Select(i => i.Code).ToList();

    [Theory]
    [InlineData("")] [InlineData("NA")] [InlineData("N/A")] [InlineData("n/a")] [InlineData("null")] [InlineData("-")]
    public void ParseCell_treats_every_missing_spelling_as_missing(string raw) =>
        Assert.Equal(CellKind.Missing, Parsing.ParseCell(raw).Kind);

    [Fact]
    public void ParseCell_handles_censored_values_lab_flags_and_text()
    {
        var c = Parsing.ParseCell("<0.2");
        Assert.Equal(CellKind.Censored, c.Kind);
        Assert.Equal(0.2, c.Value);
        Assert.Equal("<", c.Op);
        Assert.Equal(5.9, Parsing.ParseCell("5.9 H").Value);
        Assert.Equal(CellKind.Text, Parsing.ParseCell("abc").Kind);
    }

    [Fact]
    public void ParseColumn_understands_suffix_and_paren_styles()
    {
        Assert.Equal(new ParsedColumn("albumin", "max", "range_max", "gram_per_deciliter", "suffix"), Parsing.ParseColumn("albumin_range_max_gram_per_deciliter"));
        Assert.Equal("max", Parsing.ParseColumn("alt_max_units_per_liter").Kind);
        Assert.Equal(new ParsedColumn("hgb", "value", null, "g/dL", "paren"), Parsing.ParseColumn("Hgb (g/dL)"));
        Assert.Equal("min", Parsing.ParseColumn("Glucose ref low").Kind);
    }

    [Fact]
    public void ParseTime_reads_mission_timepoints()
    {
        Assert.Equal(new TimePoint("L-92", "pre", -92), Parsing.ParseTime("C001_serum_L-92"));
        Assert.Equal(new TimePoint("R+1", "post", 1), Parsing.ParseTime("C001_whole-blood_R+1_cbc"));
        Assert.Equal("inflight", Parsing.ParseTime("X01_FD12")!.Phase);
        Assert.Equal(14, Parsing.ParseTime("P01_D14")!.Day);
    }

    [Fact]
    public void Osdr_cbc_scrambled_row_is_detected_and_repaired_without_hardcoding()
    {
        var r = Load("data", "raw", "LSDS-7_Complete_Blood_Count_CBC_TRANSFORMED.csv");
        const string s = "C003_whole-blood_L-92_cbc";
        var expected = new Dictionary<string, double>
        {
            ["platelet_count"] = 330, ["mpv"] = 10.1, ["rdw"] = 12.9, ["mch"] = 29.6, ["mchc"] = 32.4,
            ["absolute_neutrophils"] = 6826, ["absolute_lymphocytes"] = 1997, ["absolute_monocytes"] = 614,
            ["absolute_eosinophils"] = 125, ["absolute_basophils"] = 38,
        };
        foreach (var (test, value) in expected) Assert.True(value == Find(r, s, test).Value, $"{test}: expected {value}, got {Find(r, s, test).Value}");
        var plt = Find(r, s, "platelet_count");
        Assert.Contains("reassigned", plt.Flags);
        Assert.Equal(12.9, plt.OriginalValue);
        Assert.Equal("g/dL", Find(r, "C001_whole-blood_L-3_cbc", "hemoglobin").Unit);
        Assert.Contains("unit_mislabel", Codes(r));
        Assert.Equal(0, r.Summary.Implausible);
    }

    [Fact]
    public void Osdr_cmp_flags_glucose_artifact_and_keeps_real_high_protein()
    {
        var r = Load("data", "raw", "LSDS-8_Comprehensive_Metabolic_Panel_CMP_TRANSFORMED.csv");
        foreach (var c in new[] { "C002", "C003", "C004" }) Assert.Contains("possible_sample_artifact", Find(r, $"{c}_serum_R+45", "glucose").Flags);
        Assert.Contains("possible_sample_artifact", Find(r, "C004_serum_R+45", "potassium").Flags);
        var tp = Find(r, "C003_serum_L-44", "total_protein");
        Assert.Equal(8.8, tp.Value);
        Assert.Contains("corroborated", tp.Flags);
        Assert.DoesNotContain(r.Records, x => x.Test.StartsWith("egfr"));
        Assert.Equal(17.3, Find(r, "C001_serum_L-44", "bun_to_creatinine_ratio").Value);
        Assert.Empty(r.Changes.Where(c => !c.Reason.StartsWith("Blank")));
    }

    [Fact]
    public void Messy_fixture_different_format_swapped_columns_typos_duplicates()
    {
        var r = Load("tools", "LabClean", "tests", "LabClean.Tests", "Fixtures", "messy_labs.csv");
        Assert.Equal(8, r.Summary.Samples);
        Assert.Contains("duplicate_row", Codes(r));
        Assert.Equal(14, Find(r, "P01_D14", "hemoglobin").Value);
        Assert.Equal(42.3, Find(r, "P01_D14", "hematocrit").Value);
        Assert.Equal(33.1, Find(r, "P01_D14", "mchc").Value);
        Assert.Contains("implausible", Find(r, "P01_D21", "glucose").Flags);
        Assert.Contains("censored_below", Find(r, "P01_D0", "crp").Flags);
        Assert.Equal(83, Find(r, "P02_D21", "glucose").Value);
        Assert.Contains("missing_tokens", Codes(r));
        Assert.Contains("unknown_test", Codes(r));
        Assert.Equal(90, r.Baselines.Baselines["P01"]["glucose"].Mean);
    }

    [Fact]
    public void Notes_use_dot_decimals_on_any_machine_language()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("bg-BG");
            var r = Load("tools", "LabClean", "tests", "LabClean.Tests", "Fixtures", "messy_labs.csv");
            Assert.Contains("42.3", r.Changes.Single(c => c.Test == "hemoglobin").Reason);
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }
}
