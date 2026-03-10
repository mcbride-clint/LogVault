using LogVault.Domain.Entities;
using System.Collections.Concurrent;

namespace LogVault.Application.Alerts;

public class AlertRuleExpressionCache
{
    private readonly ConcurrentDictionary<int, Func<LogEvent, bool>> _cache = new();

    public Func<LogEvent, bool> GetOrCompile(AlertRule rule)
    {
        return _cache.GetOrAdd(rule.Id, _ => Compile(rule.FilterExpression));
    }

    public void Invalidate(int ruleId) => _cache.TryRemove(ruleId, out _);
    public void InvalidateAll() => _cache.Clear();

    private static Func<LogEvent, bool> Compile(string expression)
    {
        try
        {
            return FilterExpressionParser.Parse(expression);
        }
        catch
        {
            return _ => false;
        }
    }
}
