namespace HealthMonitor.Api.Services.Vitals;

using System.Globalization;
using Models.Vitals;

/// <summary>
/// Rejects values that cannot be a real measurement (typos such as 1300 bpm).
/// The limits are a team demo parameter for input validation only; the ok / warning /
/// critical status still comes from vital-thresholds.json.
/// </summary>
public static class VitalPlausibility
{
    private static readonly Dictionary<string, (double Min, double Max)> Limits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["heartRate"] = (20, 250),
        ["systolicBp"] = (50, 260),
        ["diastolicBp"] = (30, 160),
        ["spo2"] = (50, 100),
        ["bodyTemp"] = (30, 45),
        ["sleepHours"] = (0, 24),
        ["mood"] = (1, 5),
    };

    /// <summary>Null when the value is plausible, otherwise a message for the user.</summary>
    public static string? Validate(VitalThreshold threshold, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return $"{threshold.Name} must be a number.";

        if (!Limits.TryGetValue(threshold.Indicator, out var limit))
            return null;

        if (value < limit.Min || value > limit.Max)
        {
            var min = limit.Min.ToString(CultureInfo.InvariantCulture);
            var max = limit.Max.ToString(CultureInfo.InvariantCulture);
            return $"{threshold.Name} must be between {min} and {max} {threshold.Unit}.";
        }

        return null;
    }
}
