namespace HealthMonitor.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class AuthAndAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    private static async Task<string?> ErrorOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
    }

    [Theory]
    [InlineData("/api/bloodpanels?crewId=C001")]
    [InlineData("/api/bloodpanels/crew")]
    [InlineData("/api/readings?crewId=C001")]
    [InlineData("/api/readings/latest?crewId=C001")]
    [InlineData("/api/nutrition?crewId=C001")]
    [InlineData("/api/crew")]
    [InlineData("/api/vitals/indicators")]
    [InlineData("/api/radiation/status")]
    [InlineData("/api/samples")]
    [InlineData("/api/external?path=planetary/apod")]
    [InlineData("/api/auth/me")]
    public async Task Anonymous_request_gets_401_json_not_redirect(string url)
    {
        var response = await factory.NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Not signed in.", await ErrorOf(response));
    }

    [Fact]
    public async Task Anonymous_post_reading_gets_401()
    {
        var response = await factory.NewClient().PostAsJsonAsync(
            "/api/readings",
            new { indicator = "heartRate", value = 70, crewId = "C001" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_endpoint_stays_public()
    {
        var response = await factory.NewClient().GetAsync("/api/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Nutrition_file_is_no_longer_public()
    {
        var response = await factory.NewClient().GetAsync("/data/nutrition-days.json");
        // No static file any more, so the authenticated fallback policy answers.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signup_creates_astronaut_even_if_role_is_sent()
    {
        var client = factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/signup", new
        {
            email = UniqueEmail(),
            password = "abcdefg1",
            displayName = "New Crew",
            role = "flight_surgeon",
            crewId = "C001",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeDto>();
        Assert.NotNull(me);
        Assert.Equal("astronaut", me.Role);
        Assert.Matches("^U[0-9]{4}$", me.CrewId);

        // Signed in by the signup call; /me agrees, and the new astronaut cannot read C001.
        var meAgain = await client.GetFromJsonAsync<MeDto>("/api/auth/me");
        Assert.Equal(me.CrewId, meAgain!.CrewId);
        Assert.Equal("astronaut", meAgain.Role);

        var other = await client.GetAsync("/api/bloodpanels?crewId=C001");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        var crewList = await client.GetAsync("/api/crew");
        Assert.Equal(HttpStatusCode.Forbidden, crewList.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "abcdefg1", "Name", "valid email")]
    [InlineData("a@b.co", "short1", "Name", "at least 8")]
    [InlineData("a@b.co", "onlyletters", "Name", "letter and one digit")]
    [InlineData("a@b.co", "12345678", "Name", "letter and one digit")]
    [InlineData("a@b.co", "abcdefg1", "  ", "Display name")]
    public async Task Signup_validation_returns_clear_400(
        string email,
        string password,
        string displayName,
        string expected)
    {
        var response = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/signup",
            new { email, password, displayName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await ErrorOf(response));
    }

    [Fact]
    public async Task Signup_with_existing_email_any_case_is_rejected()
    {
        var response = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/signup",
            new { email = "C001@BlindSpot.TEST", password = "abcdefg1", displayName = "Dup" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_wrong_password_and_unknown_email_get_same_generic_401()
    {
        var client = factory.NewClient();

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiFactory.C001Email, password = "Wrong-Password-1" });
        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = UniqueEmail(), password = "Wrong-Password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var message = await ErrorOf(wrongPassword);
        Assert.Equal("Invalid email or password.", message);
        Assert.Equal(message, await ErrorOf(unknownEmail));
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_sets_httponly_samesite_strict_cookie_and_email_is_case_insensitive()
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "C001@BLINDSPOT.test", password = ApiFactory.C001Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("blindspot.auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetFromJsonAsync<MeDto>("/api/auth/me");
        Assert.Equal("C001", me!.CrewId);
        Assert.Equal("astronaut", me.Role);
        Assert.Equal("Crew member C001", me.DisplayName);
    }

    [Fact]
    public async Task Login_over_plain_http_sets_cookie_without_secure()
    {
        var response = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiFactory.C001Email, password = ApiFactory.C001Password });

        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("blindspot.auth=", StringComparison.Ordinal));
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_behind_tls_proxy_sets_secure_cookie()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = ApiFactory.C001Email, password = ApiFactory.C001Password }),
        };
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        var response = await factory.NewClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("blindspot.auth=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Theory]
    [InlineData("/api/bloodpanels?crewId=C002")]
    [InlineData("/api/bloodpanels?crewId=c002")]
    [InlineData("/api/readings?crewId=C002")]
    [InlineData("/api/readings/latest?crewId=C002")]
    [InlineData("/api/nutrition?crewId=C002")]
    public async Task Astronaut_C001_reading_another_crew_member_gets_403(string url)
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Astronaut_own_blood_returns_200_with_only_own_rows()
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

        var explicitId = await client.GetFromJsonAsync<List<CrewRowDto>>("/api/bloodpanels?crewId=C001");
        var implicitId = await client.GetFromJsonAsync<List<CrewRowDto>>("/api/bloodpanels");

        Assert.NotEmpty(explicitId!);
        Assert.All(explicitId!, r => Assert.Equal("C001", r.CrewId));
        Assert.Equal(explicitId!.Count, implicitId!.Count);

        var crew = await client.GetFromJsonAsync<List<string>>("/api/bloodpanels/crew");
        Assert.Equal(["C001"], crew);
    }

    [Fact]
    public async Task Astronaut_own_readings_and_nutrition_return_200()
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

        var latest = await client.GetFromJsonAsync<List<CrewRowDto>>("/api/readings/latest");
        Assert.NotEmpty(latest!);
        Assert.All(latest!, r => Assert.Equal("C001", r.CrewId));

        var nutrition = await client.GetAsync("/api/nutrition");
        Assert.Equal(HttpStatusCode.OK, nutrition.StatusCode);
        var body = await nutrition.Content.ReadAsStringAsync();
        Assert.Contains("\"crewId\":\"C001\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("crew-1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Okafor", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Astronaut_cannot_post_reading_for_someone_else()
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

        var response = await client.PostAsJsonAsync(
            "/api/readings",
            new { indicator = "heartRate", value = 70, crewId = "C002" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("C001")]
    [InlineData("C002")]
    [InlineData("C003")]
    [InlineData("C004")]
    public async Task Flight_surgeon_reads_any_crew_member(string crewId)
    {
        var client = await factory.LoggedInClient(ApiFactory.SurgeonEmail, ApiFactory.SurgeonPassword);

        foreach (var url in new[]
        {
            $"/api/bloodpanels?crewId={crewId}",
            $"/api/readings?crewId={crewId}",
            $"/api/readings/latest?crewId={crewId}",
            $"/api/nutrition?crewId={crewId}",
        })
        {
            var response = await client.GetAsync(url);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{url} -> {(int)response.StatusCode}");
        }

        var blood = await client.GetFromJsonAsync<List<CrewRowDto>>($"/api/bloodpanels?crewId={crewId}");
        Assert.NotEmpty(blood!);
        Assert.All(blood!, r => Assert.Equal(crewId, r.CrewId));
    }

    [Theory]
    [InlineData("/api/bloodpanels")]
    [InlineData("/api/readings")]
    [InlineData("/api/readings/latest")]
    [InlineData("/api/nutrition")]
    public async Task Flight_surgeon_missing_crewId_gets_400(string url)
    {
        var client = await factory.LoggedInClient(ApiFactory.SurgeonEmail, ApiFactory.SurgeonPassword);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("crewId is required.", await ErrorOf(response));
    }

    [Fact]
    public async Task Flight_surgeon_unknown_crewId_gets_404()
    {
        var client = await factory.LoggedInClient(ApiFactory.SurgeonEmail, ApiFactory.SurgeonPassword);

        var response = await client.GetAsync("/api/bloodpanels?crewId=C999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Flight_surgeon_crew_list_contains_C001_to_C004()
    {
        var client = await factory.LoggedInClient(ApiFactory.SurgeonEmail, ApiFactory.SurgeonPassword);

        var crew = await client.GetFromJsonAsync<List<CrewDto>>("/api/crew");

        var ids = crew!.Select(c => c.CrewId).ToList();
        Assert.Contains("C001", ids);
        Assert.Contains("C002", ids);
        Assert.Contains("C003", ids);
        Assert.Contains("C004", ids);

        var c001 = crew!.Single(c => c.CrewId == "C001");
        Assert.True(c001.HasVitals && c001.HasBlood && c001.HasNutrition);
        var c004 = crew!.Single(c => c.CrewId == "C004");
        Assert.True(c004.HasBlood);
        Assert.False(c004.HasNutrition);

        var bloodCrew = await client.GetFromJsonAsync<List<string>>("/api/bloodpanels/crew");
        Assert.Equal(["C001", "C002", "C003", "C004"], bloodCrew);
    }

    [Fact]
    public async Task Flight_surgeon_can_log_reading_on_behalf_of_crew_member()
    {
        var client = await factory.LoggedInClient(ApiFactory.SurgeonEmail, ApiFactory.SurgeonPassword);

        var missing = await client.PostAsJsonAsync(
            "/api/readings",
            new { indicator = "heartRate", value = 71 });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var created = await client.PostAsJsonAsync(
            "/api/readings",
            new { indicator = "heartRate", value = 71, crewId = "C003" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var row = await created.Content.ReadFromJsonAsync<CrewRowDto>();
        Assert.Equal("C003", row!.CrewId);
    }

    [Theory]
    [InlineData("/api/crew")]
    [InlineData("/api/samples")]
    public async Task Astronaut_on_flight_surgeon_endpoint_gets_403_json(string url)
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Any_signed_in_user_can_read_vital_indicators()
    {
        var client = await factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);
        var response = await client.GetAsync("/api/vitals/indicators");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record MeDto(string Email, string DisplayName, string Role, string? CrewId);

    private sealed record CrewRowDto(string CrewId);

    private sealed record CrewDto(
        string CrewId,
        string DisplayName,
        bool HasVitals,
        bool HasBlood,
        bool HasNutrition);
}

public class RateLimitTests(TightRateLimitFactory factory) : IClassFixture<TightRateLimitFactory>
{
    private static Task<HttpResponseMessage> FailedLoginFrom(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "nobody@example.com", password = "Wrong-Password-1" }),
        };
        request.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Rate_limit_is_per_forwarded_client_ip_not_shared_proxy_ip()
    {
        var client = factory.NewClient();

        // Limit is 2/min: the third request from one client IP is rejected...
        await FailedLoginFrom(client, "198.51.100.1");
        await FailedLoginFrom(client, "198.51.100.1");
        var third = await FailedLoginFrom(client, "198.51.100.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);

        // ...but another visitor behind the same proxy still gets through.
        var otherVisitor = await FailedLoginFrom(client, "198.51.100.2");
        Assert.Equal(HttpStatusCode.Unauthorized, otherVisitor.StatusCode);
    }

    [Fact]
    public async Task Login_is_rate_limited_with_429_json()
    {
        var client = factory.NewClient();
        HttpResponseMessage? last = null;

        for (var i = 0; i < 3; i++)
        {
            last = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email = "nobody@example.com", password = "Wrong-Password-1" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Equal("application/json", last.Content.Headers.ContentType?.MediaType);
    }
}
