using LogVault.Domain.Entities;

namespace LogVault.Domain.Models;

public record LogStats(
    IReadOnlyList<LevelCount> ByLevel,
    IReadOnlyList<HourlyCount> ByHour
);

public record LevelCount(LogLevel Level, int Count);
public record HourlyCount(DateTimeOffset Hour, int Count);
