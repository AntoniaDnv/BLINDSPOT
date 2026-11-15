namespace HealthMonitor.Api.Services.Nutrition;

using System.Text.Json;
using System.Text.Json.Nodes;
using Data.Seeding;

/// <summary>Simulated nutrition / hydration / meds data for one crew member.</summary>
public class NutritionCrewDto
{
    public string CrewId { get; init; } = "";

    public string Note { get; init; } = "";

    /// <summary>Simulated mission role from the data file (fictional names are dropped).</summary>
    public string? Role { get; init; }

    public JsonNode? Baseline { get; init; }

    public JsonArray Days { get; init; } = [];

    public JsonArray Alerts { get; init; } = [];
}

public interface INutritionService
{
    /// <summary>Data for an already-authorised crewId; empty days/alerts if none.</summary>
    NutritionCrewDto GetForCrew(string crewId);

    IReadOnlySet<string> CrewIdsWithData { get; }
}

/// <summary>
/// Loads Data/Files/nutrition-days.json once (it is no longer served from wwwroot),
/// maps crew-1/2/3 to C001/C002/C003 and drops the fictional names.
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

    private readonly string note;
    private readonly Dictionary<string, JsonObject> crewById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<JsonObject>> daysByCrew = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<JsonObject>> alertsByCrew = new(StringComparer.Ordinal);

    public NutritionService(ILogger<NutritionService> logger)
    {
        var path = SeedFilePaths.Resolve(FileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("Nutrition data file not found: {Path}", path);
            this.note = "";
            this.CrewIdsWithData = new HashSet<string>();
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new JsonException($"{FileName} is empty.");

        this.note = root["note"]?.GetValue<string>() ?? "";

        foreach (var crew in Objects(root["crew"]))
        {
            var id = MapId(crew["id"]);
            if (id is null)
                continue;

            crew.Remove("name");
            crew["id"] = id;
            this.crewById[id] = crew;
        }

        Group(root["days"], this.daysByCrew);
        Group(root["alerts"], this.alertsByCrew);

        this.CrewIdsWithData = this.daysByCrew.Keys.ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> CrewIdsWithData { get; }

    public NutritionCrewDto GetForCrew(string crewId)
    {
        this.crewById.TryGetValue(crewId, out var crew);

        return new NutritionCrewDto
        {
            CrewId = crewId,
            Note = this.note,
            Role = crew?["role"]?.GetValue<string>(),
            Baseline = crew?["baseline"]?.DeepClone(),
            Days = Clone(this.daysByCrew, crewId),
            Alerts = Clone(this.alertsByCrew, crewId),
        };
    }

    private static JsonArray Clone(Dictionary<string, List<JsonObject>> source, string crewId)
        => source.TryGetValue(crewId, out var rows)
            ? new JsonArray(rows.Select(r => (JsonNode?)r.DeepClone()).ToArray())
            : [];

    private static void Group(JsonNode? array, Dictionary<string, List<JsonObject>> target)
    {
        foreach (var row in Objects(array))
        {
            var id = MapId(row["crewId"]);
            if (id is null)
                continue;

            row["crewId"] = id;
            if (!target.TryGetValue(id, out var list))
                target[id] = list = [];

            list.Add(row);
        }
    }

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
