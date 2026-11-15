namespace HealthMonitor.Api.Services.Nutrition;

using System.Text.Json;
using System.Text.Json.Nodes;
using Data.Seeding;
using Models.Dtos;

/// <summary>Nutrition / hydration / meds for one crew member (simulated file data + logged entries).</summary>
public class NutritionCrewDto
{
    public string CrewId { get; init; } = "";

    public string Note { get; init; } = "";

    /// <summary>Simulated mission role from the data file (fictional names are dropped).</summary>
    public string? Role { get; init; }

    public JsonNode? Baseline { get; init; }

    /// <summary>Today's mission day, so pages can find "today" in <see cref="Days"/>.</summary>
    public MissionTodayDto Today { get; init; } = new();

    public IReadOnlyList<NutritionDayDto> Days { get; init; } = [];

    public JsonArray Alerts { get; init; } = [];
}

/// <summary>One day row from the data file (used to seed NutritionDay entities).</summary>
public sealed record NutritionSeedDay(string CrewId, int Day, double Kcal, double WaterL, bool HypnoticTaken);

public interface INutritionService
{
    string Note { get; }

    /// <summary>Simulated role and baseline (targets) for a crew member, or nulls.</summary>
    (string? Role, JsonNode? Baseline) GetProfile(string crewId);

    JsonArray GetAlerts(string crewId);

    IReadOnlyList<NutritionSeedDay> SeedDays { get; }

    /// <summary>
    /// Mission day of today's date. Team demo parameter: today is the day after the last
    /// simulated day in the file, so logged entries never mix with simulated ones.
    /// </summary>
    MissionTodayDto Today { get; }

    int DayFor(DateOnly date);

    DateOnly DateFor(int day);
}

/// <summary>
/// Loads Data/Files/nutrition-days.json once (it is no longer served from wwwroot),
/// maps crew-1/2/3 to C001/C002/C003 and drops the fictional names. The day rows seed
/// the NutritionDay entities (see DataSeeder); logs then change those entities.
/// </summary>
public class NutritionService : INutritionService
{
    public const string FileName = "nutrition-days.json";

    private static readonly Dictionary<string, string> CrewIdMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crew-1"] = "C001",
        ["crew-2"] = "C002",
        ["crew-3"] = "C003",
    };

    private readonly Dictionary<string, JsonObject> crewById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<JsonObject>> alertsByCrew = new(StringComparer.Ordinal);
    private readonly DateOnly startDate;

    public NutritionService(ILogger<NutritionService> logger)
    {
        this.startDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var path = SeedFilePaths.Resolve(FileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("Nutrition data file not found: {Path}", path);
            this.Note = "";
            this.SeedDays = [];
            this.Today = new MissionTodayDto { Day = 1, Date = this.startDate };
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new JsonException($"{FileName} is empty.");

        this.Note = root["note"]?.GetValue<string>() ?? "";

        foreach (var crew in Objects(root["crew"]))
        {
            var id = MapId(crew["id"]);
            if (id is null)
                continue;

            crew.Remove("name");
            crew["id"] = id;
            this.crewById[id] = crew;
        }

        var seed = new List<NutritionSeedDay>();
        foreach (var row in Objects(root["days"]))
        {
            var id = MapId(row["crewId"]);
            if (id is null)
                continue;

            seed.Add(new NutritionSeedDay(
                id,
                row["day"]?.GetValue<int>() ?? 0,
                row["kcal"]?.GetValue<double>() ?? 0,
                row["waterL"]?.GetValue<double>() ?? 0,
                row["hypnoticTaken"]?.GetValue<bool>() ?? false));
        }

        this.SeedDays = seed;

        foreach (var row in Objects(root["alerts"]))
        {
            var id = MapId(row["crewId"]);
            if (id is null)
                continue;

            row["crewId"] = id;
            if (!this.alertsByCrew.TryGetValue(id, out var list))
                this.alertsByCrew[id] = list = [];

            list.Add(row);
        }

        var lastDay = seed.Count == 0 ? 0 : seed.Max(d => d.Day);
        this.Today = new MissionTodayDto { Day = lastDay + 1, Date = this.startDate };
    }

    public string Note { get; }

    public IReadOnlyList<NutritionSeedDay> SeedDays { get; }

    public MissionTodayDto Today { get; }

    public int DayFor(DateOnly date) => this.Today.Day + (date.DayNumber - this.startDate.DayNumber);

    public DateOnly DateFor(int day) => this.startDate.AddDays(day - this.Today.Day);

    public (string? Role, JsonNode? Baseline) GetProfile(string crewId)
        => this.crewById.TryGetValue(crewId, out var crew)
            ? (crew["role"]?.GetValue<string>(), crew["baseline"]?.DeepClone())
            : (null, null);

    public JsonArray GetAlerts(string crewId)
        => this.alertsByCrew.TryGetValue(crewId, out var rows)
            ? new JsonArray(rows.Select(r => (JsonNode?)r.DeepClone()).ToArray())
            : [];

    private static IEnumerable<JsonObject> Objects(JsonNode? array)
        => (array as JsonArray)?
            .OfType<JsonObject>()
            .Select(o => o.DeepClone().AsObject())
            .ToList()
            ?? [];

    private static string? MapId(JsonNode? node)
        => node?.GetValue<string>() is { } raw && CrewIdMap.TryGetValue(raw, out var mapped)
            ? mapped
            : null;
}
