namespace HealthMonitor.Api.Extensions;

using System.Threading.RateLimiting;
using Auth;
using Data;
using Data.Seeding;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Entities;
using Options;
using Services.BloodPanels;
using Services.External;
using Services.Nutrition;
using Services.Radiation;
using Services.Readings;
using Services.Samples;
using Services.Vitals;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureOptions(
            IConfiguration config)
            => services
                .Configure<ExternalApiOptions>(
                    config.GetSection(ExternalApiOptions.SectionName))
                .Configure<SeedingOptions>(
                    config.GetSection(SeedingOptions.SectionName))
                .Configure<AuthRateLimitOptions>(
                    config.GetSection(AuthRateLimitOptions.SectionName));

        public IServiceCollection AddInMemoryDb()
        {
            const string DefaultDbName = "AppDb";

            // "InMemoryDbName" lets tests give each test host its own database
            // (InMemory databases with the same name are shared process-wide).
            return services
                .AddDbContext<AppDbContext>(
                    (provider, options) => options.UseInMemoryDatabase(
                        provider.GetRequiredService<IConfiguration>()["InMemoryDbName"]
                            ?? DefaultDbName));
        }

        public IServiceCollection AddServices()
            => services
                .AddMemoryCache()
                .AddScoped<JsonLoader>()
                .AddScoped<CsvLoader>()
                .AddScoped<DataSeeder>()
                .AddScoped<ISampleService, SampleService>()
                .AddScoped<IBloodPanelService, BloodPanelService>()
                .AddScoped<IReadingService, ReadingService>()
                .AddSingleton<IVitalThresholdService, VitalThresholdService>()
                .AddScoped<IRadiationService, RadiationService>()
                .AddSingleton<INutritionService, NutritionService>()
                .AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>()
                .AddScoped<IAuthService, AuthService>()
                .AddScoped<ICrewAccessService, CrewAccessService>();

        /// <summary>
        /// Cookie auth (HttpOnly, SameSite=Strict, 8h sliding) that answers 401/403 with JSON
        /// instead of redirecting, plus the Astronaut / FlightSurgeon policies.
        /// Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
        /// </summary>
        public IServiceCollection AddAppAuth()
        {
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "blindspot.auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;

                    options.Events.OnRedirectToLogin = context =>
                        WriteJsonError(context.HttpContext, StatusCodes.Status401Unauthorized, "Not signed in.");
                    options.Events.OnRedirectToAccessDenied = context =>
                        WriteJsonError(context.HttpContext, StatusCodes.Status403Forbidden, "You do not have access to this resource.");
                });

            services
                .AddAuthorizationBuilder()
                .AddPolicy(Policies.Astronaut, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(Roles.Astronaut))
                .AddPolicy(Policies.FlightSurgeon, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(Roles.FlightSurgeon))
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

            return services;
        }

        /// <summary>Login/signup limit per client IP per minute (AuthRateLimit:PermitPerMinute) → 429 JSON.</summary>
        public IServiceCollection AddAuthRateLimiting()
            => services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, cancellationToken) => new ValueTask(
                    WriteJsonError(
                        context.HttpContext,
                        StatusCodes.Status429TooManyRequests,
                        "Too many attempts. Wait a minute and try again."));

                options.AddPolicy(Policies.AuthRateLimit, httpContext =>
                {
                    var permits = httpContext
                        .RequestServices
                        .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                        .Value
                        .PermitPerMinute;

                    return RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = Math.Max(1, permits),
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        });
                });
            });

        public IServiceCollection AddExternalApiHttpClient()
        {
            services
                .AddHttpClient<IExternalApiClient, ExternalApiClient>();

            return services;
        }
    }

    private static Task WriteJsonError(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new { error });
    }
}
