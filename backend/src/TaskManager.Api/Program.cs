using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TaskManager.Api.Data;
using TaskManager.Api.Extensions;
using TaskManager.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPersistence(builder.Configuration)
    .AddApplicationServices()
    .AddApiControllers()
    .AddJwtAuthentication(builder.Configuration)
    .AddSwaggerWithJwt();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// `--migrate-only`: run by the Kubernetes migration Job. Applies migrations, then exits before the
// web server starts, so replicas and blue/green colours never race to migrate at start-up.
if (args.Contains("--migrate-only"))
{
    Environment.ExitCode = await app.RunMigrationsOnlyAsync();
    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger stays on in every environment for this assessment so reviewers can try the API
// from the Docker stack. A production deployment would gate it.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness: process is up (no dependencies). Readiness: can reach the database.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

await app.ApplyMigrationsIfEnabledAsync();

app.Run();

// Exposed for WebApplicationFactory-based tests.
public partial class Program;
