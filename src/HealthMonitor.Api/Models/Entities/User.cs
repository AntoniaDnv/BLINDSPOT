namespace HealthMonitor.Api.Models.Entities;

public class User
{
    public int Id { get; init; }

    /// <summary>Email as the user typed it (trimmed).</summary>
    public string Email { get; set; } = "";

    /// <summary>Lower-case email used for the case-insensitive unique lookup.</summary>
    public string NormalizedEmail { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>PasswordHasher&lt;User&gt; output. Plaintext is never stored.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>"astronaut" or "flight_surgeon" (see Auth.Roles).</summary>
    public string Role { get; set; } = "";

    /// <summary>Linked crew member for astronauts; null for flight surgeons.</summary>
    public string? CrewId { get; set; }

    public DateTime CreatedAt { get; set; }
}
