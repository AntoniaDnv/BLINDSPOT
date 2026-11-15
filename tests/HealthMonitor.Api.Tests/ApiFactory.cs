namespace HealthMonitor.Api.Tests;

using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>Test host with a high auth rate limit so the suite never trips it.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Demo test accounts from Data/Files/demo-users.json.
    public const string C001Email = "c001@blindspot.test";
    public const string C001Password = "Dragon-Orbit-7341";
    public const string SurgeonEmail = "surgeon@blindspot.test";
    public const string SurgeonPassword = "Flight-Surgeon-9052";

    protected virtual int AuthPermitPerMinute => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("InMemoryDbName", $"TestDb-{Guid.NewGuid():N}");
        builder.UseSetting(
            "AuthRateLimit:PermitPerMinute",
            this.AuthPermitPerMinute.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Client with its own cookie container; redirects are not followed.</summary>
    public HttpClient NewClient()
        => this.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    public async Task<HttpClient> LoggedInClient(string email, string password)
    {
        var client = this.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }
}

public sealed class TightRateLimitFactory : ApiFactory
{
    protected override int AuthPermitPerMinute => 2;
}
