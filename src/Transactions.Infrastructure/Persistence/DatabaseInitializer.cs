using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Transactions.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies EF Core migrations on startup. If the project has no migrations yet
    /// (fresh clone before `dotnet ef migrations add`), falls back to EnsureCreated so the app still runs.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (db.Database.GetMigrations().Any())
        {
            logger.LogInformation("Applying EF Core migrations");
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            logger.LogWarning("No EF Core migrations found; creating schema with EnsureCreated");
            await db.Database.EnsureCreatedAsync(ct);
        }
    }
}
