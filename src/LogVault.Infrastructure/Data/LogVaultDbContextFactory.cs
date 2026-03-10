using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogVault.Infrastructure.Data;

public class LogVaultDbContextFactory : IDesignTimeDbContextFactory<LogVaultDbContext>
{
    public LogVaultDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LogVaultDbContext>()
            .UseSqlite("Data Source=logvault.db")
            .Options;
        return new LogVaultDbContext(options);
    }
}
