namespace HealthMonitor.Api.Models.Seed;

/// <summary>One row of Data/Files/demo-users.json (demo test accounts, documented in README).</summary>
public class DemoUserSeedRow
{
    public string Email { get; init; } = "";

    public string DisplayName { get; init; } = "";

    /// <summary>Test password; hashed at seed time and then discarded.</summary>
    public string Password { get; init; } = "";

    public string Role { get; init; } = "";

    public string? CrewId { get; init; }
}
