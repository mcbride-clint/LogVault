using FluentAssertions;
using LogVault.Domain.Entities;
using LogVault.Infrastructure.Data;
using LogVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogVault.Infrastructure.Tests.Repositories;

public class EfApiKeyRepositoryTests : IDisposable
{
    private readonly LogVaultDbContext _db;
    private readonly EfApiKeyRepository _repo;
    private readonly SqliteConnection _conn;

    public EfApiKeyRepositoryTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<LogVaultDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new LogVaultDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new EfApiKeyRepository(_db);
    }

    [Fact]
    public async Task CreateAsync_PersistsKey()
    {
        var key = new ApiKey { KeyHash = "abc123", Label = "Test", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow };
        var created = await _repo.CreateAsync(key);
        created.Id.Should().BeGreaterThan(0);
        _db.ApiKeys.Count().Should().Be(1);
    }

    [Fact]
    public async Task FindByHashAsync_ReturnsKey()
    {
        await _repo.CreateAsync(new ApiKey { KeyHash = "testhash", Label = "Test", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow });
        var found = await _repo.FindByHashAsync("testhash");
        found.Should().NotBeNull();
        found!.Label.Should().Be("Test");
    }

    [Fact]
    public async Task FindByHashAsync_UnknownHash_ReturnsNull()
    {
        var found = await _repo.FindByHashAsync("doesnotexist");
        found.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_SetsRevokedAndDisabled()
    {
        var key = await _repo.CreateAsync(new ApiKey { KeyHash = "h1", Label = "K1", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow });
        await _repo.RevokeAsync(key.Id);

        await _db.Entry(key).ReloadAsync();
        key.IsRevoked.Should().BeTrue();
        key.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RotateAsync_SetsExpiryOnOldKeyAndLinksNewKey()
    {
        var oldKey = await _repo.CreateAsync(new ApiKey
        {
            KeyHash = "oldhash", Label = "OldKey", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow
        });

        var newKeyEntity = new ApiKey
        {
            KeyHash = "newhash", Label = "NewKey", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow
        };

        var (created, expired) = await _repo.RotateAsync(oldKey.Id, newKeyEntity);

        created.Id.Should().BeGreaterThan(0);
        created.RotatedFromId.Should().Be(oldKey.Id);
        expired.ExpiresAt.Should().NotBeNull();
        expired.ExpiresAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(23));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
