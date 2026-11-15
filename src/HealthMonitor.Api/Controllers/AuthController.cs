namespace HealthMonitor.Api.Controllers;

using System.Globalization;
using System.Security.Claims;
using Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Models.Dtos;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Creates an astronaut account (role cannot be chosen) and signs it in.</summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<MeResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MeResponseDto>> Signup(
        SignupRequestDto dto,
        CancellationToken cancellationToken)
    {
        var result = await auth.SignupAsync(
            dto.Email,
            dto.Password,
            dto.DisplayName,
            cancellationToken);

        if (!result.Succeeded)
            return this.BadRequest(new { error = result.Error });

        await this.SignInAsync(result.User!);

        return this.StatusCode(StatusCodes.Status201Created, ToMe(result.User!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<MeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MeResponseDto>> Login(
        LoginRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = await auth.ValidateCredentialsAsync(
            dto.Email,
            dto.Password,
            cancellationToken);

        if (user is null)
        {
            return this.Unauthorized(new { error = AuthService.InvalidCredentialsMessage });
        }

        await this.SignInAsync(user);

        return this.Ok(ToMe(user));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await this.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return this.NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<MeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponseDto>> Me(CancellationToken cancellationToken)
    {
        var idValue = this.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return this.Unauthorized(new { error = "Not signed in." });

        var user = await auth.FindByIdAsync(id, cancellationToken);
        if (user is null)
            return this.Unauthorized(new { error = "Not signed in." });

        return this.Ok(ToMe(user));
    }

    private static MeResponseDto ToMe(Models.Entities.User user)
        => new()
        {
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role,
            CrewId = user.CrewId,
        };

    private Task SignInAsync(Models.Entities.User user)
        => this.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            auth.CreatePrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme),
            new AuthenticationProperties { IsPersistent = true });
}
