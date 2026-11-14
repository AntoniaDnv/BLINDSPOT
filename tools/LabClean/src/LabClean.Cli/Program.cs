using LabClean.Core;

const string Help = """
Assess and clean lab CSV files.

  dotnet run --project tools/LabClean/src/LabClean.Cli -- <file.csv> [more.csv ...] [options]

Options:
  --out <dir>             output folder (default: data/clean)
  --assess-only           write only the assessment, no cleaned data
  --no-repair             do not move misplaced values between columns
  --id-column <name>      column holding the sample ID (auto-detected)
  --subject-column <name> column holding the subject ID (else read from sample ID)
  --visit-column <name>   column holding the visit/timepoint (else read from sample ID)
  --subject-regex <re>    regex for the subject inside the sample ID; use a group named "subject"
  --strict                exit with code 1 if any error-level issue is found

For each input <name>.csv it writes <out>/<name>/:
  ASSESSMENT.md     human-readable report (issues, changes, quality)
  assessment.json   the same, machine-readable
  records.json      tidy long data: one record per sample × test   (not with --assess-only)
  baselines.json    per-subject pre-mission mean/SD per test       (not with --assess-only)
""";

var files = new List<string>();
string outDir = "data/clean";
string? idColumn = null, subjectColumn = null, visitColumn = null, subjectRegex = null;
bool assessOnly = false, repair = true, strict = false;

try
{
    for (var i = 0; i < args.Length; i++)
    {
        string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
        switch (args[i])
        {
            case "--out": outDir = Next(); break;
            case "--assess-only": assessOnly = true; break;
            case "--no-repair": repair = false; break;
            case "--id-column": idColumn = Next(); break;
            case "--subject-column": subjectColumn = Next(); break;
            case "--visit-column": visitColumn = Next(); break;
            case "--subject-regex": subjectRegex = Next(); break;
            case "--strict": strict = true; break;
            case "-h" or "--help": Console.WriteLine(Help); return 0;
            case var a when a.StartsWith("--"): throw new ArgumentException($"Unknown option {a}");
            default: files.Add(args[i]); break;
        }
    }
}
catch (ArgumentException e)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}

if (files.Count == 0) { Console.WriteLine(Help); return 2; }

Console.OutputEncoding = System.Text.Encoding.UTF8;
var options = new CleanOptions { IdColumn = idColumn, SubjectColumn = subjectColumn, VisitColumn = visitColumn, SubjectRegex = subjectRegex, Repair = repair };
var icon = new Dictionary<string, string> { ["error"] = "🔴", ["warning"] = "🟡", ["info"] = "🔵" };
var errors = 0;

foreach (var file in files)
{
    if (!File.Exists(file)) { Console.Error.WriteLine($"File not found: {file}"); return 2; }
    var name = Path.GetFileNameWithoutExtension(file);
    var result = LabCleaner.Run(File.ReadAllText(file), options);
    var dir = Path.Combine(outDir, name);
    Report.WriteAll(result, Path.GetFileName(file), dir, assessOnly);

    var s = result.Summary;
    errors += s.Issues.Error;
    Console.WriteLine($"\n{Path.GetFileName(file)} -> {dir}");
    Console.WriteLine($"  {s.Samples} samples, {s.Subjects} subjects, {s.Tests} tests, {s.Cells} values");
    Console.WriteLine($"  missing {s.Missing} · changed {s.Changed} · implausible {s.Implausible} · outliers {s.Outliers} · artifacts {s.Artifacts}");
    Console.WriteLine(FormattableString.Invariant($"  clean values: {s.CleanCellsPercent}%   issues: {s.Issues.Error} error, {s.Issues.Warning} warning, {s.Issues.Info} info"));
    foreach (var i in result.Issues.Where(x => x.Severity != "info")) Console.WriteLine($"  {icon[i.Severity]} {i.Code}: {i.Message}");
}

return strict && errors > 0 ? 1 : 0;
