namespace HealthMonitor.Api.Auth;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

public sealed record AuthResult(User? User, string? Error)
{
    public bool Succeeded => this.User is not null;
}

public interface IAuthService
{
    Task<AuthResult> SignupAsync(
        string? email,
        string? password,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<User?> ValidateCredentialsAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    ClaimsPrincipal CreatePrincipal(User user, string authenticationScheme);
}

public partial class AuthService(
    AppDbContext data,
    IPasswordHasher<User> hasher) : IAuthService
{
    public const int MinPasswordLength = 8;

    // Caps the work done by the hasher on hostile input.
    public const int MaxPasswordLength = 128;

    public const int MaxEmailLength = 254;

    public const int MaxDisplayNameLength = 60;

    // Same message for unknown email and wrong password (no user enumeration).
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    // Serialises the "email free? crewId free? insert" sequence; the InMemory
    // provider does not enforce unique indexes.
    private static readonly SemaphoreSlim SignupLock = new(1, 1);

    // Verified against when the email is unknown so both paths cost one hash check.
    private static readonly Lazy<string> DummyHash = new(
        () => new PasswordHasher<User>().HashPassword(new User(), "dummy-password-1"));

    public static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    public static string? ValidateSignup(string? email, string? password, string? displayName)
    {
        var trimmedEmail = email?.Trim() ?? "";
        if (trimmedEmail.Length == 0)
            return "Email is required.";

        if (trimmedEmail.Length > MaxEmailLength || !EmailPattern().IsMatch(trimmedEmail))
            return "Enter a valid email address (like name@example.com).";

        if (string.IsNullOrEmpty(password))
            return "Password is required.";

        if (password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters.";

        if (password.Length > MaxPasswordLength)
            return $"Password must be at most {MaxPasswordLength} characters.";

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must contain at least one letter and one digit.";

        var name = displayName?.Trim() ?? "";
        if (name.Length == 0)
            return "Display name is required.";

        if (name.Length > MaxDisplayNameLength)
            return $"Display name must be at most {MaxDisplayNameLength} characters.";

        return null;
    }

    public async Task<AuthResult> SignupAsync(
        string? email,
        string? password,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateSignup(email, password, displayName);
        if (error is not null)
            return new AuthResult(null, error);

        var normalized = NormalizeEmail(email!);

        await SignupLock.WaitAsync(cancellationToken);
        try
        {
            var taken = await data
                .Users
                .AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken);

            if (taken)
            {
                return new AuthResult(
                    null,
                    "This email cannot be used to create an account. If it is yours, log in instead.");
            }

            var user = new User
            {
                Email = email!.Trim(),
                NormalizedEmail = normalized,
                DisplayName = displayName!.Trim(),
                Role = Roles.Astronaut,
                CrewId = await this.NewCrewIdAsync(cancellationToken),
                CreatedAt = DateTime.UtcNow,
            };
            user.PasswordHash = hasher.HashPassword(user, password!);

            data.Users.Add(user);
            await data.SaveChangesAsync(cancellationToken);

            return new AuthResult(user, null);
        }
        finally
        {
            SignupLock.Release();
        }
    }

    public async Task<User?> ValidateCredentialsAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrEmpty(password)
            || password.Length > MaxPasswordLength)
        {
            return null;
        }

        var normalized = NormalizeEmail(email);
        var user = await data
            .Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (user is null)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash.Value, password);
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await data.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public Task<User?> FindByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
        => data
            .Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public ClaimsPrincipal CreatePrincipal(User user, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role),
        };

        if (!string.IsNullOrEmpty(user.CrewId))
            claims.Add(new Claim(AppClaims.CrewId, user.CrewId));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationScheme));
    }

    private async Task<string> NewCrewIdAsync(CancellationToken cancellationToken)
    {
        // "U" + 4 digits. Random first, then a sequential scan if we keep colliding.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = $"U{RandomNumberGenerator.GetInt32(0, 10000):D4}";
            if (!await data.Users.AnyAsync(u => u.CrewId == candidate, cancellationToken))
                return candidate;
        }

        var used = await data
            .Users
            .Where(u => u.CrewId != null && u.CrewId.StartsWith("U"))
            .Select(u => u.CrewId!)
            .ToListAsync(cancellationToken);

        var usedSet = used.ToHashSet(StringComparer.Ordinal);
        for (var n = 0; n < 10000; n++)
        {
            var candidate = $"U{n:D4}";
            if (!usedSet.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException("No free astronaut crewIds left.");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex EmailPattern();
}
