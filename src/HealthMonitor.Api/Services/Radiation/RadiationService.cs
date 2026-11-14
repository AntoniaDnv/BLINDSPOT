namespace HealthMonitor.Api.Services.Radiation;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Models;
using Models.Dtos;
using Services.External;

public class RadiationService(IExternalApiClient nasa, ILogger<RadiationService> logger)
    : IRadiationService
{
    public async Task<RadiationStatusResponseDto> GetStatusAsync(
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken cancellationToken = default)
    {
        // Determine date range
        var endDate = end ?? DateTime.UtcNow;
        var startDate = start
            ?? endDate.AddDays(-30); // default 30 days if days param not specified

        try
        {
            var startDateStr = startDate.ToString("yyyy-MM-dd");
            var endDateStr = endDate.ToString("yyyy-MM-dd");

            var events = await nasa.Get<List<DonkiSepEvent>>(
                $"DONKI/SEP?startDate={startDateStr}&endDate={endDateStr}",
                cancellationToken);

            // Handle null/empty response
            if (events == null || events.Count == 0)
            {
                logger.LogInformation("No SEP events found for {Start} to {End}", startDateStr, endDateStr);
                return new RadiationStatusResponseDto
                {
                    Status = "ok",
                    Guidance = "No solar particle events detected.",
                    EventCount = 0,
                };
            }

            logger.LogInformation("Found {Count} SEP event records for {Start} to {End}", events.Count, startDateStr, endDateStr);

            // Group events into storms
            var storms = GroupIntoStorms(events);

            // Find the highest energy across all storms
            var maxEnergy = storms
                .SelectMany(s => s.Events)
                .SelectMany(e => e.Instruments)
                .Select(i => ParseEnergy(i.DisplayName))
                .Where(e => e.HasValue)
                .DefaultIfEmpty(0)
                .Max();

            // Determine status
            var (status, guidance) = maxEnergy switch
            {
                >= 100 => ("critical", "Move to the most shielded module, no EVAs, report to flight surgeons."),
                >= 10 => ("warning", "Postpone EVAs, monitor dosimeter."),
                _ => ("ok", "No solar particle events detected.")
            };

            // Count unique flare and CME events
            var allLinkedEvents = storms
                .SelectMany(s => s.Events)
                .SelectMany(e => e.LinkedEvents ?? [])
                .Select(le => le.ActivityId)
                .Distinct()
                .ToList();

            var flareCount = allLinkedEvents.Count(id => id.Contains("-FLR-"));
            var cmeCount = allLinkedEvents.Count(id => id.Contains("-CME-"));

            // Find the last event time
            var lastEventTime = storms
                .SelectMany(s => s.Events)
                .Select(e => DateTimeOffset.Parse(e.EventTime))
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max()
                .ToString("yyyy-MM-ddTHH:mmZ");

            return new RadiationStatusResponseDto
            {
                Status = status,
                Guidance = guidance,
                EventCount = storms.Count,
                LastEventTime = lastEventTime,
                FlareCount = flareCount,
                CmeCount = cmeCount,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch radiation data from NASA DONKI");
            return new RadiationStatusResponseDto
            {
                Status = "error",
                Guidance = "Radiation data unavailable.",
                Error = ex.Message,
            };
        }
    }

    private static List<RadiationStorm> GroupIntoStorms(List<DonkiSepEvent> events)
    {
        var storms = new List<RadiationStorm>();

        foreach (var evt in events)
        {
            // Parse event time for grouping
            if (!DateTimeOffset.TryParse(evt.EventTime, out var eventTime))
                continue;

            // Try to find an existing storm for this event
            var matchedStorm = storms
                .FirstOrDefault(s => s.IsWithinWindow(eventTime, evt));

            if (matchedStorm != null)
            {
                matchedStorm.AddEvent(evt);
                continue;
            }

            // Create a new storm
            storms.Add(new RadiationStorm(evt));
        }

        return storms;
    }

    private static double? ParseEnergy(string displayName)
    {
        // Extract energy values from displayName like:
        // "GOES-P: SEISS >100 MeV"
        // "GOES-P: SEISS >10 MeV"
        // "MODEL: REleASE:SOHO/EPHIN 15.8-39.8 MeV"
        // "SOHO: COSTEP 28.2-50.1 MeV"
        // "STEREO A: IMPACT 13-100 MeV"

        // Look for ">X MeV" pattern first (highest energy indicator)
        var gtMatch = Regex.Match(displayName, @">(\d+(?:\.\d+)?)\s*MeV", RegexOptions.IgnoreCase);
        if (gtMatch.Success && double.TryParse(gtMatch.Groups[1].Value, out var gtEnergy))
            return gtEnergy;

        // Look for "X-Y MeV" range pattern - take the max
        var rangeMatch = Regex.Match(displayName, @"(\d+(?:\.\d+)?)\s*[-–]\s*(\d+(?:\.\d+)?)\s*MeV", RegexOptions.IgnoreCase);
        if (rangeMatch.Success && double.TryParse(rangeMatch.Groups[2].Value, out var rangeMax))
            return rangeMax;

        return null;
    }
}

// Internal helper class for storm grouping
internal class RadiationStorm(DonkiSepEvent firstEvent)
{
    private readonly List<DonkiSepEvent> _events = [firstEvent];
    private readonly DateTimeOffset _firstTime = DateTimeOffset.Parse(firstEvent.EventTime);
    private readonly HashSet<string> _linkedActivityIds = [];

    public IReadOnlyList<DonkiSepEvent> Events => _events.AsReadOnly();

    public void AddEvent(DonkiSepEvent evt)
    {
        _events.Add(evt);

        // Track linked activity IDs for grouping
        if (evt.LinkedEvents != null)
        {
            foreach (var linked in evt.LinkedEvents)
                _linkedActivityIds.Add(linked.ActivityId);
        }
    }

    public bool IsWithinWindow(DateTimeOffset newTime, DonkiSepEvent newEvent)
    {
        // Group if within 6 hours of any event in the storm
        var timeDiff = (newTime - _firstTime).TotalHours;
        if (timeDiff >= 0 && timeDiff <= 6)
            return true;

        // Group if shares linked activity IDs (same flare/CME source)
        if (newEvent.LinkedEvents != null)
        {
            foreach (var linked in newEvent.LinkedEvents)
            {
                if (_linkedActivityIds.Contains(linked.ActivityId))
                    return true;
            }
        }

        return false;
    }
}
