namespace HealthMonitor.Api.Auth;

using System.Security.Claims;
using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Outcome of resolving which crew member a data request is about.
/// Either <see cref="CrewId"/> is set (allowed) or an error status + message is.
/// </summary>
public sealed record CrewAccessResult(string? CrewId, int StatusCode, string? Error)
{
    public bool IsAllowed => this.CrewId is not null;

    public static CrewAccessResult Allow(string crewId)
        => new(crewId, StatusCodes.Status200OK, null);

    public static CrewAccessResult Deny(int statusCode, string error)
        => new(null, statusCode, error);

    /// <summary>JSON error response for a denied result.</summary>
    public ObjectResult ToErrorResult()
        => new(new { error = this.Error }) { StatusCode = this.StatusCode };
}

public interface ICrewAccessService
{
    /// <summary>
    /// Astronaut: always their own crewId (asking for another crew member → 403).
    /// Flight surgeon: the requested crewId (missing → 400, unknown → 404).
    /// Anyone else → 403.
    /// </summary>
    Task<CrewAccessResult> ResolveAsync(
        ClaimsPrincipal user,
        string? requestedCrewId,
        CancellationToken cancellationToken = default);

    /// <summary>All known crewIds (astronaut accounts), sorted.</summary>
    Task<IReadOnlyList<string>> GetAllCrewIdsAsync(
        CancellationToken cancellationToken = default);
}

public class CrewAccessService(AppDbContext data) : ICrewAccessService
{
    public async Task<CrewAccessResult> ResolveAsync(
        ClaimsPrincipal user,
        string? requestedCrewId,
        CancellationToken cancellationToken = default)
    {
        var requested = string.IsNullOrWhiteSpace(requestedCrewId)
            ? null
            : requestedCrewId.Trim();

        if (user.IsInRole(Roles.Astronaut))
        {
            // The crewId comes only from the signed (encrypted) auth cookie,
            // never from the query string or request body.
            var ownCrewId = user.FindFirstValue(AppClaims.CrewId);
            if (string.IsNullOrEmpty(ownCrewId))
            {
                return CrewAccessResult.Deny(
                    StatusCodes.Status403Forbidden,
                    "Your account is not linked to a crew member.");
            }

            if (requested is not null
                && !string.Equals(requested, ownCrewId, StringComparison.OrdinalIgnoreCase))
            {
                return CrewAccessResult.Deny(
                    StatusCodes.Status403Forbidden,
                    "Astronauts can only access their own health data.");
            }

            return CrewAccessResult.Allow(ownCrewId);
        }

        if (user.IsInRole(Roles.FlightSurgeon))
        {
            if (requested is null)
            {
                return CrewAccessResult.Deny(
                    StatusCodes.Status400BadRequest,
                    "crewId is required.");
            }

            var normalized = requested.ToUpperInvariant();
            var exists = await data
                .Users
                .AsNoTracking()
                .AnyAsync(
                    u => u.Role == Roles.Astronaut && u.CrewId == normalized,
                    cancellationToken);

            return exists
                ? CrewAccessResult.Allow(normalized)
                : CrewAccessResult.Deny(
                    StatusCodes.Status404NotFound,
                    $"Unknown crew member '{normalized}'.");
        }

        return CrewAccessResult.Deny(
            StatusCodes.Status403Forbidden,
            "Your role cannot access crew health data.");
    }

    public async Task<IReadOnlyList<string>> GetAllCrewIdsAsync(
        CancellationToken cancellationToken = default)
        => await data
            .Users
            .AsNoTracking()
            .Where(u => u.Role == Roles.Astronaut && u.CrewId != null)
            .Select(u => u.CrewId!)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
}
