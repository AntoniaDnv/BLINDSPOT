namespace HealthMonitor.Api.Options;

public class AuthRateLimitOptions
{
    public const string SectionName = "AuthRateLimit";

    /// <summary>Login + signup requests allowed per client IP per minute.</summary>
    public int PermitPerMinute { get; set; } = 10;
}
