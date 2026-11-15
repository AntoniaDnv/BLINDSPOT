namespace HealthMonitor.Api.Auth;

public static class Roles
{
    public const string Astronaut = "astronaut";

    public const string FlightSurgeon = "flight_surgeon";
}

public static class Policies
{
    public const string Astronaut = "Astronaut";

    public const string FlightSurgeon = "FlightSurgeon";

    /// <summary>Rate-limiter policy for login and signup.</summary>
    public const string AuthRateLimit = "auth";
}

public static class AppClaims
{
    public const string CrewId = "crew_id";
}
