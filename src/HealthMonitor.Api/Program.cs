using HealthMonitor.Api.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Personal, git-ignored overrides (e.g. a real API key).
// Optional: the app runs without it.
builder
    .Configuration
    .AddJsonFile(
        "appsettings.Local.json",
        optional: true);

builder
    .Services
    .ConfigureOptions(builder.Configuration)
    .AddInMemoryDb()
    .AddServices()
    .AddAppAuth()
    .AddAuthRateLimiting()
    .AddExternalApiHttpClient()
    .AddOpenApi()
    .AddProblemDetails()
    .AddControllers();

var app = builder.Build();

var cancellationToken = app
    .Lifetime
    .ApplicationStopping;

await app.SeedData(cancellationToken);

app.UseExceptionHandler();
app.UseStatusCodePages();

// Static files (the SPA shell) are public and served before authorization runs.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference().AllowAnonymous();

app.MapControllers();

await app.RunAsync(cancellationToken);
