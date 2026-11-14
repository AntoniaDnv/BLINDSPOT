using System.Text.RegularExpressions;

namespace LabClean.Core;

/// <summary>
/// Generic lab-data assessor and cleaner.
/// <para>Input: a wide table (one row per sample; columns like <c>test_value_unit</c>,
/// <c>test_range_min_unit</c>, <c>Hemoglobin (g/dL)</c>, <c>Glucose ref low</c>).
/// Output: tidy long records, per-subject baselines and a list of issues.</para>
/// <para>Pipeline: parse → schema → units → duplicates → anomalies → identity checks →
/// reassign misplaced values → identity fill → final flags → ranges → visit clusters → baselines.
/// Every change keeps the original value and a note explaining why.</para>
/// </summary>
public static class LabCleaner
{
    public const double ZFlag = 4;     // robust z above this (vs population AND own history) = outlier
    public const double ZAccept = 3.5; // a relocated value must fit its new column at least this well
    private const double OpenBoundShare = 0.8; // range column this empty = "no bound", not "missing"
    private const int ClusterMin = 3;          // subjects abnormal in the same direction at one visit
    private const int MaxReassignTargets = 12;

    private static readonly string[] BaselineExclude =
        ["missing", "implausible", "possible_sample_artifact", "suspected_misalignment", "identity_mismatch", "duplicate_sample_id"];

    // ---------- internal state ----------

    private sealed class SchemaEntry
    {
        public required string Test { get; init; }
        public bool Known { get; init; }
        public Dictionary<string, string> Cols { get; } = [];
        public string UnitRaw { get; set; } = "";
    }

    private sealed class Cell
    {
        public double? Value;
        public double? RefMin, RefMax;
        public bool RefMinOpen, RefMaxOpen;
        public readonly HashSet<string> Flags = [];
        public readonly List<string> Notes = [];
        public bool HasOriginal;
        public double? OriginalValue;
        public string? Status;
    }

    private sealed class Sample
    {
        public required string Id { get; init; }
        public int Row { get; init; }
        public required string Subject { get; init; }
        public string? Visit { get; init; }
        public string? Phase { get; init; }
        public int? Day { get; init; }
        public Dictionary<string, Cell> Cells { get; } = [];
        public List<(double Value, string From)> Displaced { get; } = [];
        public HashSet<string> Corroborated { get; } = [];
    }

    private sealed record Stats(double Med, double Scale);

    // ---------- helpers ----------

    /// <summary>Rounds half away from zero for positives, matching JavaScript Math.round.</summary>
    private static double Round(double x, int d)
    {
        var f = Math.Pow(10, d);
        return Math.Floor(x * f + 0.5) / f;
    }

    private static double? Median(IEnumerable<double> values)
    {
        var s = values.OrderBy(v => v).ToArray();
        if (s.Length == 0) return null;
        return s.Length % 2 == 1 ? s[(s.Length - 1) / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    private static Stats? Robust(List<double> values)
    {
        if (values.Count < 3) return null;
        var med = Median(values)!.Value;
        var mad = Median(values.Select(v => Math.Abs(v - med)))!.Value;
        return new(med, Math.Max(Math.Max(mad * 1.4826, Math.Abs(med) * 0.03), 1e-6));
    }

    /// <summary>Formats numbers with "." whatever the machine's language settings are.</summary>
    private static string Inv(FormattableString s) => FormattableString.Invariant(s);

    private static double? Z(double v, Stats? st) => st is null ? null : Math.Abs(v - st.Med) / st.Scale;

    private static bool Implausible(string test, double v) =>
        Knowledge.Tests.TryGetValue(test, out var t) && (v < t.Min || v > t.Max);

    private static readonly Regex IdHeaderRe = new(@"^(sample([ _]?(name|id))?|specimen[ _]?id|id)$", RegexOptions.IgnoreCase);
    private static readonly Regex SubjectHeaderRe = new(@"^(subject|participant|crew|crew[ _]?id|patient|subject[ _]?id)$", RegexOptions.IgnoreCase);
    private static readonly Regex VisitHeaderRe = new(@"^(visit|timepoint|time[ _]?point|day|study[ _]?day|flight[ _]?day)$", RegexOptions.IgnoreCase);

    // ---------- entry points ----------

    public static CleanResult Run(string csvText, CleanOptions? options = null)
    {
        var table = Parsing.ParseCsv(csvText);
        return Run(table.Header, table.Rows, options);
    }

    public static CleanResult Run(List<string> header, List<Dictionary<string, string>> rows, CleanOptions? options = null)
    {
        var opts = options ?? new CleanOptions();
        var issues = new List<Issue>();
        var changes = new List<Change>();

        void AddIssue(string severity, string code, string message, IEnumerable<string>? examples = null)
        {
            var ex = (examples ?? []).Distinct().ToList();
            issues.Add(new Issue { Severity = severity, Code = code, Message = message, Count = ex.Count > 0 ? ex.Count : null, Examples = ex.Take(8).ToList() });
        }
        string Raw(Dictionary<string, string> row, string col) => row.GetValueOrDefault(col) ?? "";

        // ---- 1. sample ID column and optional subject/visit columns ----
        var idCol = opts.IdColumn
            ?? header.FirstOrDefault(h => IdHeaderRe.IsMatch(h.Trim()))
            ?? header.FirstOrDefault(h => rows.Count(r => !Parsing.IsNumericCell(Raw(r, h))) >= rows.Count * 0.8)
            ?? header.FirstOrDefault() ?? "";
        var subjectCol = opts.SubjectColumn ?? header.FirstOrDefault(h => h != idCol && SubjectHeaderRe.IsMatch(h.Trim()));
        var visitCol = opts.VisitColumn ?? header.FirstOrDefault(h => h != idCol && VisitHeaderRe.IsMatch(h.Trim()));
        var metaCols = new HashSet<string>(new[] { idCol, subjectCol, visitCol }.OfType<string>());

        // ---- 2. schema: map columns to tests ----
        var schema = new Dictionary<string, SchemaEntry>();
        var testOrder = new List<string>();
        var dropped = new List<(string Col, string Reason)>();
        var ignored = new List<string>();
        var aliased = new List<string>();
        var kindWords = new Dictionary<string, List<string>> { ["min"] = [], ["max"] = [] };

        foreach (var col in header)
        {
            if (metaCols.Contains(col)) continue;
            var pc = Parsing.ParseColumn(col);
            if (pc.Style == "bare" && !rows.Any(r => Parsing.IsNumericCell(Raw(r, col)))) { ignored.Add(col); continue; }
            var drop = Knowledge.DropRules.FirstOrDefault(d => d.Pattern.IsMatch(pc.Test));
            if (drop is not null) { dropped.Add((col, drop.Reason)); continue; }

            var known = Knowledge.TestAliases.TryGetValue(pc.Test, out var canonical);
            var name = known ? canonical! : pc.Test;
            if (known && name != pc.Test) aliased.Add($"{col} → {name}");
            if (!schema.TryGetValue(name, out var entry))
            {
                entry = new SchemaEntry { Test = name, Known = known };
                schema[name] = entry;
                testOrder.Add(name);
            }
            if (entry.Cols.TryGetValue(pc.Kind, out var existing))
                AddIssue("error", "duplicate_column", $"Two columns map to {name} {pc.Kind}; using the first.", [existing, col]);
            else entry.Cols[pc.Kind] = col;
            if (pc.Kind == "value") entry.UnitRaw = pc.UnitRaw;
            if (pc.KindWord is not null && kindWords.TryGetValue(pc.Kind, out var words) && !words.Contains(pc.KindWord)) words.Add(pc.KindWord);
        }
        foreach (var test in testOrder.ToList())
        {
            var e = schema[test];
            if (e.Cols.ContainsKey("value")) continue;
            AddIssue("warning", "range_without_value", $"Reference range columns for {test} have no matching value column; ignored.", e.Cols.Values);
            schema.Remove(test);
            testOrder.Remove(test);
        }
        var tests = testOrder;

        foreach (var reason in dropped.Select(d => d.Reason).Distinct())
            AddIssue("info", "column_dropped", $"Dropped columns: {reason}", dropped.Where(d => d.Reason == reason).Select(d => d.Col));
        if (ignored.Count > 0) AddIssue("info", "column_ignored", "Non-numeric columns kept as metadata only.", ignored);
        if (aliased.Count > 0) AddIssue("info", "test_renamed", "Test names mapped to a standard name.", aliased);
        foreach (var (kind, words) in kindWords)
            if (words.Count > 1)
                AddIssue("info", "inconsistent_naming", $"Reference-range {kind} columns use different spellings ({string.Join(", ", words)}); all normalised.", words);
        var unknown = tests.Where(t => !schema[t].Known).ToList();
        if (unknown.Count > 0) AddIssue("info", "unknown_test", "Tests not in the knowledge base: no plausibility limits or unit check applied.", unknown);

        // ---- 3. samples and cells ----
        var openCols = new HashSet<string>();
        foreach (var e in schema.Values)
            foreach (var k in new[] { "min", "max" })
                if (e.Cols.TryGetValue(k, out var col) && rows.Count(r => Parsing.ParseCell(Raw(r, col)).Kind == CellKind.Missing) >= rows.Count * OpenBoundShare)
                    openCols.Add(col);
        if (openCols.Count > 0)
            AddIssue("info", "open_bound", "Range columns that are almost always empty are treated as \"no limit\" (e.g. eGFR has no upper limit).", openCols);

        var missingTokens = new Dictionary<string, int>();
        var seenRaw = new Dictionary<string, int>();
        var samples = new List<Sample>();
        List<string> textValues = [], censored = [], strippedText = [], unparsedIds = [];

        for (var idx = 0; idx < rows.Count; idx++)
        {
            var row = rows[idx];
            var idRaw = Raw(row, idCol).Trim();
            var id = idRaw != "" ? idRaw : $"row-{idx + 2}";
            var rawKey = string.Join("\u001f", header.Select(h => Raw(row, h)));
            if (seenRaw.TryGetValue(rawKey, out var firstIdx))
            {
                AddIssue("warning", "duplicate_row", $"Row {idx + 2} is an exact copy of row {firstIdx + 2}; dropped.", [id]);
                continue;
            }
            seenRaw[rawKey] = idx;

            var time = Parsing.ParseTime(visitCol is not null ? Raw(row, visitCol) : id) ?? (visitCol is not null ? Parsing.ParseTime(id) : null);
            var subjectRaw = subjectCol is not null ? Raw(row, subjectCol).Trim() : "";
            var subject = subjectRaw != "" ? subjectRaw : Parsing.ParseSubject(id, opts.SubjectRegex);
            if (subject is null || time is null) unparsedIds.Add(id);
            var sample = new Sample { Id = id, Row = idx + 2, Subject = subject ?? id, Visit = time?.Visit, Phase = time?.Phase, Day = time?.Day };

            foreach (var test in tests)
            {
                var e = schema[test];
                var cell = new Cell();
                var v = Parsing.ParseCell(Raw(row, e.Cols["value"]));
                switch (v.Kind)
                {
                    case CellKind.Missing:
                        cell.Flags.Add("missing");
                        var key = v.Token == "" ? "(empty)" : v.Token!;
                        missingTokens[key] = missingTokens.GetValueOrDefault(key) + 1;
                        break;
                    case CellKind.Text:
                        cell.Flags.Add("missing"); cell.Flags.Add("unparseable");
                        textValues.Add($"{id} {test}: \"{v.Token}\"");
                        break;
                    default:
                        cell.Value = v.Value;
                        if (v.Kind == CellKind.Censored)
                        {
                            cell.Flags.Add(v.Op!.StartsWith('<') ? "censored_below" : "censored_above");
                            censored.Add($"{id} {test}: {v.Token}");
                        }
                        if (v.Kind == CellKind.NumberWithText)
                        {
                            cell.Flags.Add("stripped_text");
                            strippedText.Add($"{id} {test}: \"{v.Token}\"");
                        }
                        break;
                }
                foreach (var k in new[] { "min", "max" })
                {
                    if (!e.Cols.TryGetValue(k, out var col)) continue;
                    var r = Parsing.ParseCell(Raw(row, col));
                    if (r.Kind is CellKind.Missing or CellKind.Text)
                    {
                        if (openCols.Contains(col)) { if (k == "min") cell.RefMinOpen = true; else cell.RefMaxOpen = true; }
                        else if (cell.Value is not null) cell.Flags.Add("range_missing");
                    }
                    else if (k == "min") cell.RefMin = r.Value;
                    else cell.RefMax = r.Value;
                }
                sample.Cells[test] = cell;
            }
            samples.Add(sample);
        }

        foreach (var group in samples.GroupBy(s => s.Id).Where(g => g.Count() > 1))
        {
            AddIssue("error", "duplicate_sample_id", $"Sample ID {group.Key} appears {group.Count()} times with different values; kept all, flagged.", [group.Key]);
            foreach (var s in group) foreach (var c in s.Cells.Values) c.Flags.Add("duplicate_sample_id");
        }
        if (missingTokens.Count > 0)
            AddIssue(missingTokens.Count > 1 ? "warning" : "info", "missing_tokens",
                $"Missing values are written {missingTokens.Count} different way(s): {string.Join(", ", missingTokens.Select(kv => $"{kv.Key} ×{kv.Value}"))}. All treated as missing.");
        if (textValues.Count > 0) AddIssue("warning", "unparseable_value", "Text found where a number was expected; treated as missing.", textValues);
        if (censored.Count > 0) AddIssue("info", "censored_value", "Values reported as \"<x\" or \">x\"; stored as x and flagged.", censored);
        if (strippedText.Count > 0) AddIssue("info", "stripped_text", "Numbers with trailing text (e.g. lab flags \"H\"/\"L\"); text removed.", strippedText);
        if (unparsedIds.Count > 0) AddIssue("warning", "unparsed_sample_id", "Could not read subject and/or visit from these sample IDs. Use a subject regex or a visit column.", unparsedIds);

        // ---- 4. units ----
        var units = new Dictionary<string, string>();
        foreach (var test in tests)
        {
            var e = schema[test];
            Knowledge.Tests.TryGetValue(test, out var info);
            var hasCanon = Knowledge.UnitAliases.TryGetValue(Knowledge.NormUnitKey(e.UnitRaw), out var canon);
            var unit = hasCanon ? canon! : e.UnitRaw;
            if (e.UnitRaw != "" && !hasCanon)
                AddIssue("warning", "unknown_unit", $"Unit \"{e.UnitRaw}\" for {test} not recognised; kept as written.", [e.Cols["value"]]);
            if (info is not null && hasCanon && canon != info.Unit)
            {
                var vals = samples.Select(s => s.Cells[test].Value).OfType<double>().ToList();
                var fit = vals.Count(v => v >= info.Min && v <= info.Max) / (double)Math.Max(vals.Count, 1);
                if (fit >= 0.9)
                {
                    AddIssue("warning", "unit_mislabel", $"{test} is labelled \"{canon}\" but its values are typical {info.Unit}; relabelled to {info.Unit}.", [e.Cols["value"]]);
                    unit = info.Unit;
                }
                else
                    AddIssue("error", "unit_mismatch", $"{test} is in {canon}, expected {info.Unit}. Values were NOT converted; convert before comparing to other data.", [e.Cols["value"]]);
            }
            else if (info is not null && e.UnitRaw == "")
            {
                unit = info.Unit;
                if (info.Unit != "ratio") AddIssue("info", "unit_assumed", $"No unit given for {test}; assumed {info.Unit}.", [e.Cols["value"]]);
            }
            units[test] = unit;
        }

        // ---- 5. anomaly scoring ----
        var globalStats = new Dictionary<string, Stats?>();
        void ComputeGlobal()
        {
            foreach (var t in tests)
                globalStats[t] = Robust(samples.Select(s => s.Cells[t].Value).OfType<double>().Where(v => !Implausible(t, v)).ToList());
        }
        Stats? SubjectStats(Sample sample, string test) => Robust(samples
            .Where(s => s != sample && s.Subject == sample.Subject)
            .Select(s => s.Cells[test].Value).OfType<double>().Where(v => !Implausible(test, v)).ToList());
        bool IsAnomalous(Sample sample, string test)
        {
            if (!sample.Cells.TryGetValue(test, out var c) || c.Value is not double v) return false;
            if (Implausible(test, v)) return true;
            var zg = Z(v, globalStats[test]);
            var zs = Z(v, SubjectStats(sample, test));
            return zg > ZFlag && (zs is null || zs > ZFlag);
        }
        double FitCost(Sample sample, string test, double v)
        {
            if (Implausible(test, v)) return double.PositiveInfinity;
            var zg = Z(v, globalStats[test]);
            var zs = Z(v, SubjectStats(sample, test));
            if (zg is null && zs is null) return 1;
            return Math.Min(zg ?? double.PositiveInfinity, zs ?? double.PositiveInfinity);
        }
        void SetValue(Sample sample, string test, double value, string flag, string note)
        {
            var c = sample.Cells[test];
            if (!c.HasOriginal) { c.HasOriginal = true; c.OriginalValue = c.Value; }
            changes.Add(new Change(sample.Id, test, c.Value, value, note));
            c.Value = value;
            c.Flags.Remove("missing");
            c.Flags.Add(flag);
            c.Notes.Add(note);
        }

        // ---- 6/8. identity checks: pass 1 may recompute; pass 2 only fills and flags ----
        void RunIdentities(int pass)
        {
            foreach (var s in samples)
            {
                var bad = tests.ToDictionary(t => t, t => IsAnomalous(s, t));
                foreach (var id in Knowledge.Identities)
                {
                    if (!s.Cells.TryGetValue(id.Target, out var t) || id.Inputs.Any(n => !s.Cells.ContainsKey(n))) continue;
                    var ins = id.Inputs.Select(n => s.Cells[n].Value).ToArray();
                    if (ins.Any(v => v is null)) continue;
                    var expected = Round(id.Compute(ins.Select(v => v!.Value).ToArray()), id.Decimals);
                    if (!double.IsFinite(expected)) continue;
                    var inputsBad = id.Inputs.Any(n => bad[n]);
                    if (t.Value is null)
                    {
                        if (!inputsBad) SetValue(s, id.Target, expected, "filled_from_identity", $"Blank; filled as {id.Formula}.");
                        continue;
                    }
                    var diff = Math.Abs(t.Value.Value - expected);
                    if (diff <= Math.Max(id.Rel * Math.Abs(expected), id.Abs))
                    {
                        s.Corroborated.Add(id.Target);
                        foreach (var n in id.Inputs) s.Corroborated.Add(n);
                        continue;
                    }
                    if (pass == 1 && !inputsBad)
                    {
                        s.Displaced.Add((t.Value.Value, id.Target));
                        SetValue(s, id.Target, expected, "recomputed_from_identity", Inv($"{t.Value} did not match {id.Formula} = {expected}; recomputed."));
                        bad[id.Target] = false;
                    }
                    else
                    {
                        t.Flags.Add("identity_mismatch");
                        t.Notes.Add(Inv($"Does not match {id.Formula} = {expected}."));
                    }
                }
            }
        }
        ComputeGlobal();
        RunIdentities(1);

        // ---- 7. put misplaced values back where they fit ----
        if (opts.Repair)
        {
            foreach (var s in samples)
            {
                var targets = tests.Where(t => IsAnomalous(s, t) && !s.Corroborated.Contains(t) && !s.Cells[t].Flags.Contains("recomputed_from_identity")).ToList();
                var pool = targets.Select(t => (Value: s.Cells[t].Value!.Value, From: t)).Concat(s.Displaced).ToList();
                if (targets.Count == 0 || pool.Count < 2 || targets.Count > MaxReassignTargets) continue;

                var cand = targets.Select(t => pool
                    .Select((p, i) => (I: i, Cost: p.From == t ? double.PositiveInfinity : FitCost(s, t, p.Value)))
                    .Where(c => c.Cost <= ZAccept).OrderBy(c => c.Cost).ToList()).ToList();
                var order = Enumerable.Range(0, targets.Count).OrderBy(k => cand[k].Count).ToList();
                (double Total, int[] Pick)? best = null;
                var used = new HashSet<int>();
                var pick = new int[targets.Count];
                void Dfs(int depth, double total)
                {
                    if (best is not null && total >= best.Value.Total) return;
                    if (depth == order.Count) { best = (total, (int[])pick.Clone()); return; }
                    var k = order[depth];
                    foreach (var c in cand[k])
                    {
                        if (!used.Add(c.I)) continue;
                        pick[k] = c.I;
                        Dfs(depth + 1, total + c.Cost);
                        used.Remove(c.I);
                    }
                }
                Dfs(0, 0);

                if (best is not null)
                {
                    var moves = targets.Select((t, k) => (T: t, P: pool[best.Value.Pick[k]])).ToList();
                    foreach (var (t, p) in moves)
                        SetValue(s, t, p.Value, "reassigned", Inv($"{s.Cells[t].Value} was implausible here; {p.Value} (found in {p.From}) fits this test."));
                    AddIssue("error", "misplaced_values",
                        $"Sample {s.Id}: values were in the wrong columns; moved back ({string.Join(", ", moves.Select(m => Inv($"{m.T} ← {m.P.Value} from {m.P.From}")))}). Verify against the source.", [s.Id]);
                }
                else if (targets.Count >= 3)
                {
                    foreach (var t in targets)
                    {
                        s.Cells[t].Flags.Add("suspected_misalignment");
                        s.Cells[t].Notes.Add("Several implausible values in one row; columns may be shifted.");
                    }
                    AddIssue("error", "suspected_misalignment", $"Sample {s.Id}: {targets.Count} implausible values could not be matched to other columns; check this row by hand.", [s.Id]);
                }
            }
        }

        ComputeGlobal();
        RunIdentities(2);

        void Summarise(string flag, string severity, string code, string message)
        {
            var ex = samples.SelectMany(s => tests.Where(t => s.Cells[t].Flags.Contains(flag)).Select(t => $"{s.Id} {t}")).ToList();
            if (ex.Count > 0) AddIssue(severity, code, message, ex);
        }
        Summarise("filled_from_identity", "info", "filled_from_identity", "Blank values filled from other tests using standard formulas.");
        Summarise("recomputed_from_identity", "error", "recomputed_from_identity", "Values contradicted standard formulas while their inputs looked fine; recomputed.");
        Summarise("identity_mismatch", "warning", "identity_mismatch", "Values that contradict a standard formula, left unchanged because an input also looks wrong.");

        // ---- 9. final plausibility / outlier flags ----
        ComputeGlobal();
        foreach (var s in samples)
            foreach (var t in tests)
            {
                var c = s.Cells[t];
                if (c.Value is not double v) continue;
                if (Implausible(t, v)) c.Flags.Add("implausible");
                else if (IsAnomalous(s, t)) c.Flags.Add("outlier");
                if (s.Corroborated.Contains(t)) c.Flags.Add("corroborated");
            }
        Summarise("implausible", "error", "implausible_value", "Physiologically impossible values (outside hard limits); kept but excluded from baselines.");
        Summarise("outlier", "warning", "outlier", $"Unusual vs both the population and the subject's own other visits (robust z > {ZFlag}). \"corroborated\" = consistent with related tests, so probably real.");
        Summarise("range_missing", "info", "range_missing", "Values with no reference range in that row.");

        // ---- 10. status vs reference range ----
        foreach (var s in samples)
            foreach (var t in tests)
            {
                var c = s.Cells[t];
                var hasRange = c.RefMin is not null || c.RefMax is not null || c.RefMinOpen || c.RefMaxOpen;
                c.Status = c.Value is not double v ? null
                    : !hasRange ? "no_range"
                    : c.RefMin is double lo && v < lo ? "low"
                    : c.RefMax is double hi && v > hi ? "high"
                    : "normal";
            }

        // ---- 11. reference ranges that change for one subject ----
        var rangeChanged = new List<string>();
        var subjects = samples.Select(s => s.Subject).Distinct().ToList();
        foreach (var subj in subjects)
        {
            var mine = samples.Where(s => s.Subject == subj).ToList();
            foreach (var t in tests)
            {
                var keyed = mine.Where(s => s.Cells[t].RefMin is not null || s.Cells[t].RefMax is not null)
                    .Select(s => (S: s, Key: $"{s.Cells[t].RefMin}|{s.Cells[t].RefMax}")).ToList();
                if (keyed.Select(k => k.Key).Distinct().Count() < 2) continue;
                var pool = keyed.Any(k => k.S.Phase == "pre") ? keyed.Where(k => k.S.Phase == "pre").ToList() : keyed;
                var reference = pool.GroupBy(k => k.Key).OrderByDescending(g => g.Count()).First().Key;
                foreach (var k in keyed.Where(k => k.Key != reference)) k.S.Cells[t].Flags.Add("reference_range_changed");
                rangeChanged.Add($"{subj} {t}");
            }
        }
        if (rangeChanged.Count > 0)
            AddIssue("warning", "reference_range_changed", "Reference range changes between visits for the same subject (different lab or sex-specific range). Compare to the personal baseline, not the lab range.", rangeChanged);

        // ---- 12. many subjects abnormal at one visit ----
        var visits = samples.Select(s => s.Visit).OfType<string>().Distinct().ToList();
        foreach (var visit in visits)
        {
            var at = samples.Where(s => s.Visit == visit).ToList();
            foreach (var t in tests)
            {
                var withVal = at.Where(s => s.Cells[t].Value is not null).ToList();
                var nWith = withVal.Select(s => s.Subject).Distinct().Count();
                foreach (var status in new[] { "low", "high" })
                {
                    var hit = withVal.Where(s => s.Cells[t].Status == status).ToList();
                    var nSubj = hit.Select(s => s.Subject).Distinct().Count();
                    if (nSubj < ClusterMin || nSubj < (int)Math.Ceiling(0.5 * nWith)) continue;
                    var rule = Knowledge.PreanalyticalRules.FirstOrDefault(r => r.Trigger.Test == t && r.Trigger.Status == status);
                    if (rule is not null)
                    {
                        var flagged = new List<string>();
                        foreach (var s in hit) { s.Cells[t].Flags.Add("possible_sample_artifact"); flagged.Add($"{s.Id} {t}"); }
                        foreach (var comp in rule.Companions)
                            foreach (var s in at)
                                if (s.Cells.TryGetValue(comp.Test, out var cc) && cc.Status == comp.Status)
                                {
                                    cc.Flags.Add("possible_sample_artifact");
                                    flagged.Add($"{s.Id} {comp.Test}");
                                }
                        AddIssue("warning", "possible_sample_artifact", $"{visit}: {nSubj} subjects have {status} {t}. {rule.Reason} Do not alert on these.", flagged);
                    }
                    else
                    {
                        foreach (var s in hit) s.Cells[t].Flags.Add("visit_cluster");
                        AddIssue("info", "visit_cluster", $"{visit}: {nSubj} subjects have {status} {t}. Could be a real effect (e.g. after spaceflight) or sample handling; review.", hit.Select(s => s.Id));
                    }
                }
            }
        }

        // ---- 13. personal baselines ----
        bool Usable(Cell c) => c.Value is not null && !BaselineExclude.Any(c.Flags.Contains)
            && !(c.Flags.Contains("outlier") && !c.Flags.Contains("corroborated"));
        var basePick = samples.Where(s => s.Phase == "pre").ToList();
        string? baseMethod = "pre-flight visits (phase = pre)";
        if (basePick.Count == 0) { basePick = samples.Where(s => s.Day is <= 0).ToList(); baseMethod = "visits with day ≤ 0"; }
        var baselines = new Dictionary<string, Dictionary<string, Baseline>>();
        if (basePick.Count > 0)
        {
            foreach (var s in basePick)
                foreach (var t in tests)
                {
                    var c = s.Cells[t];
                    if (!Usable(c)) continue;
                    if (!baselines.TryGetValue(s.Subject, out var bySubject)) baselines[s.Subject] = bySubject = [];
                    if (!bySubject.TryGetValue(t, out var b)) bySubject[t] = b = new Baseline { Unit = units[t] };
                    b.Values.Add(c.Value!.Value);
                    b.Visits.Add(s.Visit);
                }
            foreach (var b in baselines.Values.SelectMany(x => x.Values))
            {
                var n = b.Values.Count;
                var mean = b.Values.Average();
                b.N = n;
                b.Mean = Round(mean, 3);
                b.Sd = n > 1 ? Round(Math.Sqrt(b.Values.Sum(v => (v - mean) * (v - mean)) / (n - 1)), 3) : null;
            }
        }
        else
        {
            baseMethod = null;
            AddIssue("info", "no_baseline", "No pre-mission visits found (no \"L-\", \"pre\" or day ≤ 0 in sample IDs); baselines not computed.");
        }

        // ---- 14. records ----
        var records = new List<LabRecord>();
        foreach (var s in samples)
            foreach (var t in tests)
            {
                var c = s.Cells[t];
                var rec = new LabRecord
                {
                    Subject = s.Subject, SampleId = s.Id, Visit = s.Visit, Phase = s.Phase, Day = s.Day,
                    Test = t, Value = c.Value, Unit = units[t],
                    RefMin = c.RefMin, RefMax = c.RefMax, RefMinOpen = c.RefMinOpen, RefMaxOpen = c.RefMaxOpen,
                    Status = c.Status, Flags = c.Flags.OrderBy(f => f, StringComparer.Ordinal).ToList(),
                };
                if (c.HasOriginal || c.Notes.Count > 0)
                {
                    rec.Extra = [];
                    if (c.HasOriginal) rec.Extra["originalValue"] = c.OriginalValue;
                    if (c.Notes.Count > 0) rec.Extra["notes"] = c.Notes.ToList();
                }
                records.Add(rec);
            }
        records = records
            .OrderBy(r => r.Subject, StringComparer.Ordinal)
            .ThenBy(r => r.Day ?? int.MaxValue)
            .ThenBy(r => r.Visit ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.SampleId, StringComparer.Ordinal)
            .ThenBy(r => r.Test, StringComparer.Ordinal)
            .ToList();

        // ---- summary ----
        int CountFlag(string f) => records.Count(r => r.Flags.Contains(f));
        var clean = records.Count(r => r.Flags.All(f => f == "corroborated"));
        var severityOrder = new Dictionary<string, int> { ["error"] = 0, ["warning"] = 1, ["info"] = 2 };
        var sortedIssues = issues.OrderBy(i => severityOrder[i.Severity]).ToList();

        return new CleanResult
        {
            Summary = new Summary
            {
                Rows = rows.Count, Samples = samples.Count, Subjects = subjects.Count, Visits = visits.Count, Tests = tests.Count,
                Cells = records.Count, Missing = CountFlag("missing"), Changed = records.Count(r => r.Changed),
                Implausible = CountFlag("implausible"), Outliers = CountFlag("outlier"), Artifacts = CountFlag("possible_sample_artifact"),
                CleanCellsPercent = records.Count > 0 ? Round(100.0 * clean / records.Count, 1) : 100,
                Issues = new IssueCounts
                {
                    Error = issues.Count(i => i.Severity == "error"),
                    Warning = issues.Count(i => i.Severity == "warning"),
                    Info = issues.Count(i => i.Severity == "info"),
                },
            },
            Issues = sortedIssues,
            Changes = changes,
            Records = records,
            Baselines = new BaselineSet
            {
                Method = baseMethod is null ? null : $"Mean and sample SD per subject and test over {baseMethod}, excluding missing, implausible, unexplained outliers and suspected artifacts.",
                Baselines = baselines,
            },
            Schema = tests.ToDictionary(t => t, t => new ColumnInfo
            {
                Columns = new Dictionary<string, string>(schema[t].Cols),
                Unit = units[t],
                UnitAsWritten = schema[t].UnitRaw,
                Known = schema[t].Known,
            }),
            IdColumn = idCol,
        };
    }
}
