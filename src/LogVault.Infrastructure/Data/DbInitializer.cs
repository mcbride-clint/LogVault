using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogVault.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(LogVaultDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        // Enable WAL mode and NORMAL synchronous for SQLite
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
            await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);
            logger.LogInformation("SQLite WAL mode enabled");
        }
    }
}
