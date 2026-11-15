namespace HealthMonitor.Api.Models.Dtos;

// No [Required]/[EmailAddress] attributes on purpose: AuthService validates and
// returns one clear { error } message instead of a ProblemDetails dictionary.
// There is deliberately no Role or CrewId property: signup always creates an astronaut.

public class SignupRequestDto
{
    public string? Email { get; init; }

    public string? Password { get; init; }

    public string? DisplayName { get; init; }
}

public class LoginRequestDto
{
    public string? Email { get; init; }

    public string? Password { get; init; }
}

public class MeResponseDto
{
    public string Email { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Role { get; init; } = "";

    public string? CrewId { get; init; }
}
