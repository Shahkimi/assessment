using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations when Database:ApplyMigrationsOnStartup is true.
    /// Enabled in docker-compose / k8s so the stack works from a cold start; off by default
    /// so a plain `dotnet run` never alters a database unexpectedly.
    /// </summary>
    public static async Task ApplyMigrationsIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            return;

        await ApplyMigrationsAsync(app);
    }

    /// <summary>
    /// One-shot mode for the Kubernetes migration Job (`--migrate-only`): applies pending migrations
    /// regardless of Database:ApplyMigrationsOnStartup, then the process exits without serving traffic.
    /// Returns the process exit code (0 = database is up to date, 1 = migration failed).
    /// </summary>
    public static async Task<int> RunMigrationsOnlyAsync(this WebApplication app)
    {
        try
        {
            await ApplyMigrationsAsync(app);
            return 0;
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Migration failed.");
            return 1;
        }
    }

    private static async Task ApplyMigrationsAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        await WaitForDatabaseAsync(db, logger);

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        logger.LogInformation("Applying {Count} pending migration(s): {Names}", pending.Count, string.Join(", ", pending));

        await db.Database.MigrateAsync();
        logger.LogInformation("Database is up to date.");
    }

    /// <summary>
    /// Npgsql's retry strategy does not treat "connection refused" as transient, so a database that
    /// is still starting (Kubernetes has no depends_on) would crash the API on its first attempt.
    /// Poll for up to ~60 s instead; Kubernetes' startupProbe tolerates this wait.
    /// </summary>
    private static async Task WaitForDatabaseAsync(AppDbContext db, ILogger logger)
    {
        const int maxAttempts = 30;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await db.Database.CanConnectAsync())
                return;

            logger.LogWarning("Database not reachable yet (attempt {Attempt}/{Max}); retrying in 2 s.", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new InvalidOperationException("Database was not reachable after 60 seconds.");
    }
}
