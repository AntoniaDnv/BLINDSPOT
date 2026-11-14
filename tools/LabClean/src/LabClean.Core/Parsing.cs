using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LabClean.Core;

public enum CellKind { Missing, Number, Censored, NumberWithText, Text }

/// <summary>One raw cell, interpreted.</summary>
/// <param name="Op">For censored values: "&lt;", "&lt;=", "&gt;" or "&gt;=".</param>
public sealed record ParsedCell(CellKind Kind, double? Value = null, string? Op = null, string? Token = null, string? Text = null);

/// <param name="Kind">"value", "min" or "max".</param>
/// <param name="KindWord">How the header spelled the kind, e.g. "range_max" or "max".</param>
/// <param name="Style">"suffix" (test_value_unit), "paren" (Name (unit)) or "bare".</param>
public sealed record ParsedColumn(string Test, string Kind, string? KindWord, string UnitRaw, string Style);

/// <param name="Phase">"pre", "post", "inflight" or null.</param>
public sealed record TimePoint(string Visit, string? Phase, int? Day);

public sealed record CsvTable(List<string> Header, List<Dictionary<string, string>> Rows);

public static class Parsing
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string NormTest(string s) =>
        Regex.Replace(s.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

    // ---------- CSV ----------

    public static CsvTable ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cur = new StringBuilder();
        var quoted = false;
        var src = text.TrimStart('﻿');

        void EndRow()
        {
            row.Add(cur.ToString()); cur.Clear();
            if (row.Any(c => c.Trim() != "")) rows.Add(row);
            row = [];
        }

        for (var i = 0; i < src.Length; i++)
        {
            var ch = src[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < src.Length && src[i + 1] == '"') { cur.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cur.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cur.ToString()); cur.Clear(); }
            else if (ch is '\n' or '\r')
            {
                if (ch == '\r' && i + 1 < src.Length && src[i + 1] == '\n') i++;
                EndRow();
            }
            else cur.Append(ch);
        }
        EndRow();

        if (rows.Count == 0) return new([], []);
        var header = rows[0];
        var body = rows.Skip(1)
            .Select(cells => header.Select((h, i) => (h, v: i < cells.Count ? cells[i] : ""))
                .GroupBy(x => x.h).ToDictionary(g => g.Key, g => g.First().v))
            .ToList();
        return new(header, body);
    }

    // ---------- cells ----------

    private static readonly Regex CensoredRe = new(@"^([<>]=?)\s*(-?\d*\.?\d+(?:e[+-]?\d+)?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberRe = new(@"^-?\d*\.?\d+(?:e[+-]?\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberTextRe = new(@"^(-?\d*\.?\d+)\s*([a-z*!]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled); // "5.9 H", "12*"

    private static double Num(string s) => double.Parse(s, NumberStyles.Float, Inv);

    public static ParsedCell ParseCell(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (Knowledge.MissingTokens.Contains(s.ToLowerInvariant())) return new(CellKind.Missing, Token: s);
        var c = CensoredRe.Match(s);
        if (c.Success) return new(CellKind.Censored, Num(c.Groups[2].Value), c.Groups[1].Value, s);
        if (NumberRe.IsMatch(s)) return new(CellKind.Number, Num(s));
        var t = NumberTextRe.Match(s);
        if (t.Success) return new(CellKind.NumberWithText, Num(t.Groups[1].Value), Token: s, Text: t.Groups[2].Value);
        return new(CellKind.Text, Token: s);
    }

    public static bool IsNumericCell(string? raw) =>
        ParseCell(raw).Kind is CellKind.Number or CellKind.Censored or CellKind.NumberWithText;

    // ---------- column headers ----------

    private static readonly Dictionary<string, string> StrongKinds = new()
    {
        ["value"] = "value", ["result"] = "value", ["range_min"] = "min", ["range_max"] = "max",
        ["ref_min"] = "min", ["ref_max"] = "max", ["ref_low"] = "min", ["ref_high"] = "max",
    };
    private static readonly Dictionary<string, string> WeakKinds = new()
    {
        ["min"] = "min", ["max"] = "max", ["lower"] = "min", ["upper"] = "max", ["low"] = "min", ["high"] = "max",
    };

    private static Regex KindRe(Dictionary<string, string> kinds) =>
        new($"^(.+?)_({string.Join("|", kinds.Keys.OrderByDescending(k => k.Length))})(?:_(.+))?$", RegexOptions.Compiled);

    private static readonly Regex StrongRe = KindRe(StrongKinds);
    private static readonly Regex WeakRe = KindRe(WeakKinds);
    private static readonly Regex ParenRe = new(@"^(.*?)\s*[(\[]([^)\]]*)[)\]]\s*$", RegexOptions.Compiled);

    /// <summary>Understands "albumin_range_max_gram_per_deciliter", "Hgb (g/dL)", "Glucose ref low" ...</summary>
    public static ParsedColumn ParseColumn(string col)
    {
        string name = col, style = "suffix";
        string? unitRaw = null;
        var p = ParenRe.Match(col);
        if (p.Success) { name = p.Groups[1].Value; unitRaw = p.Groups[2].Value; style = "paren"; }
        var light = Regex.Replace(name.Trim().ToLowerInvariant(), @"\s+", "_");
        foreach (var (re, kinds) in new[] { (StrongRe, StrongKinds), (WeakRe, WeakKinds) })
        {
            var m = re.Match(light);
            if (m.Success)
                return new(NormTest(m.Groups[1].Value), kinds[m.Groups[2].Value], m.Groups[2].Value,
                    unitRaw ?? (m.Groups[3].Success ? m.Groups[3].Value : ""), style);
        }
        return new(NormTest(name), "value", null, unitRaw ?? "", p.Success ? "paren" : "bare");
    }

    // ---------- sample IDs ----------

    private const string Sep = @"(?:^|[_\-\s])";
    private const string End = @"(?=$|[_\-\s])";
    private static readonly RegexOptions Ci = RegexOptions.IgnoreCase | RegexOptions.Compiled;
    private static string StripSep(string s) => Regex.Replace(s, @"^[_\-\s]", "");
    private static int I(string s) => int.Parse(s, NumberStyles.AllowLeadingSign, Inv);

    private static readonly (Regex Re, Func<Match, TimePoint> F)[] TimePatterns =
    [
        (new Regex($@"{Sep}([LR])([+-]\d+){End}", Ci),
            m => new($"{m.Groups[1].Value.ToUpperInvariant()}{m.Groups[2].Value}", m.Groups[1].Value.ToUpperInvariant() == "L" ? "pre" : "post", I(m.Groups[2].Value))),
        (new Regex($@"{Sep}FD[_-]?(\d+){End}", Ci), m => new($"FD{m.Groups[1].Value}", "inflight", I(m.Groups[1].Value))),
        (new Regex($@"{Sep}(pre(?:flight)?|baseline)[_-]?(\d+)?{End}", Ci),
            m => new(StripSep(m.Value), "pre", m.Groups[2].Success ? -I(m.Groups[2].Value) : null)),
        (new Regex($@"{Sep}(post(?:flight)?|recovery)[_-]?(\d+)?{End}", Ci),
            m => new(StripSep(m.Value), "post", m.Groups[2].Success ? I(m.Groups[2].Value) : null)),
        (new Regex($@"{Sep}(?:day|d)[_-]?(-?\d+){End}", Ci), m => new($"D{m.Groups[1].Value}", null, I(m.Groups[1].Value))),
    ];

    /// <summary>Reads L-92 / R+1 (pre/post mission), FD12 (flight day), pre/post, D14 / day14.</summary>
    public static TimePoint? ParseTime(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (var (re, f) in TimePatterns)
        {
            var m = re.Match(text);
            if (m.Success) return f(m);
        }
        return null;
    }

    private static readonly Regex DefaultSubjectRe = new(@"^([A-Za-z]{1,4}\d{1,4})(?=$|[_\-\s])", RegexOptions.Compiled);

    /// <summary>Subject ID from a sample ID, e.g. "C001" from "C001_serum_L-3". A custom regex may use a group named "subject".</summary>
    public static string? ParseSubject(string id, string? subjectRegex = null)
    {
        var re = subjectRegex is null ? DefaultSubjectRe : new Regex(subjectRegex);
        var m = re.Match(id);
        if (!m.Success) return null;
        if (m.Groups["subject"].Success) return m.Groups["subject"].Value;
        return m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1].Value : m.Value;
    }
}
