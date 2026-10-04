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

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        logger.LogInformation("Applying {Count} pending migration(s): {Names}", pending.Count, string.Join(", ", pending));

        // Npgsql retry-on-failure covers a database that is still starting up.
        await db.Database.MigrateAsync();
        logger.LogInformation("Database is up to date.");
    }
}
