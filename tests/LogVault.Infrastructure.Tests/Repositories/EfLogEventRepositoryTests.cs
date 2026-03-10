using FluentAssertions;
using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Infrastructure.Data;
using LogVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogVault.Infrastructure.Tests.Repositories;

public class EfLogEventRepositoryTests : IDisposable
{
    private readonly LogVaultDbContext _db;
    private readonly EfLogEventRepository _repo;
    private readonly SqliteConnection _conn;

    public EfLogEventRepositoryTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<LogVaultDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new LogVaultDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new EfLogEventRepository(_db);
    }

    private LogEvent MakeEvent(LogLevel level = LogLevel.Information, string? app = "TestApp",
        DateTimeOffset? timestamp = null) => new()
    {
        Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        Level = level,
        MessageTemplate = "Test message",
        RenderedMessage = "Test message",
        SourceApplication = app,
        PropertiesJson = "{}",
        IngestedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task BulkInsertAsync_InsertsAllEvents()
    {
        var events = Enumerable.Range(0, 5).Select(_ => MakeEvent()).ToList();
        await _repo.BulkInsertAsync(events);
        _db.LogEvents.Count().Should().Be(5);
    }

    [Fact]
    public async Task QueryAsync_FiltersByLevel()
    {
        await _repo.BulkInsertAsync(new[]
        {
            MakeEvent(LogLevel.Information),
            MakeEvent(LogLevel.Error),
            MakeEvent(LogLevel.Warning)
        });

        var query = LogEventQuery.Default with { MinLevel = LogLevel.Error };
        var result = await _repo.QueryAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public async Task QueryAsync_FiltersByApplication()
    {
        await _repo.BulkInsertAsync(new[]
        {
            MakeEvent(app: "AppA"),
            MakeEvent(app: "AppB"),
            MakeEvent(app: "AppA")
        });

        var query = LogEventQuery.Default with { SourceApplication = "AppA" };
        var result = await _repo.QueryAsync(query);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_PaginationWorks()
    {
        await _repo.BulkInsertAsync(Enumerable.Range(0, 10).Select(_ => MakeEvent()).ToList());

        var query = LogEventQuery.Default with { Page = 1, PageSize = 3 };
        var result = await _repo.QueryAsync(query);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_DeletesCorrectEvents()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-10);
        var recent = DateTimeOffset.UtcNow;

        await _repo.BulkInsertAsync(new[]
        {
            MakeEvent(timestamp: old),
            MakeEvent(timestamp: old),
            MakeEvent(timestamp: recent)
        });

        var cutoff = DateTimeOffset.UtcNow.AddDays(-5);
        var deleted = await _repo.DeleteOlderThanAsync(cutoff);

        deleted.Should().Be(2);
        _db.LogEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task QueryAsync_FiltersByTraceId()
    {
        var e1 = MakeEvent(); e1.TraceId = "abc123"; await _repo.InsertAsync(e1);
        var e2 = MakeEvent(); e2.TraceId = "xyz789"; await _repo.InsertAsync(e2);

        var query = LogEventQuery.Default with { TraceId = "abc123" };
        var result = await _repo.QueryAsync(query);
        result.Items.Should().HaveCount(1);
        result.Items[0].TraceId.Should().Be("abc123");
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
