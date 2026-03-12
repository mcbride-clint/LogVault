using LogVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LogVault.Infrastructure.Data;

public class LogVaultDbContext : DbContext
{
    public LogVaultDbContext(DbContextOptions<LogVaultDbContext> options) : base(options) { }

    public DbSet<LogEvent> LogEvents => Set<LogEvent>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertRecipient> AlertRecipients => Set<AlertRecipient>();
    public DbSet<AlertFired> AlertsFired => Set<AlertFired>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(LogVaultDbContext).Assembly);

        // Store all DateTimeOffset as Int64 (UTC ticks) so SQLite can sort/compare them.
        var converter = new DateTimeOffsetToBinaryConverter();
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(converter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
