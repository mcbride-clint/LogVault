using LogVault.Domain.Models;
using LogVault.Domain.Entities;

namespace LogVault.Application.Parsing;

/// <summary>
/// Parses a SQL-like query expression into structured filter fields that can be
/// applied as EF Core WHERE clauses.
///
/// Supported syntax:
///   level >= Warning
///   app == "PaymentService"
///   message contains "timeout"
///   exception contains "SqlException"
///   prop:UserId == "42"
///   prop:RequestPath contains "/api"
///   timestamp > "2024-01-01T00:00:00Z"
///   expr AND expr AND expr   (AND only — OR is not yet supported)
/// </summary>
public static class LogQueryExpressionParser
{
    public static ParsedQueryExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new ParsedQueryExpression();

        try
        {
            var tokens = Tokenize(expression);
            var pos = 0;
            var result = new ParsedQueryExpression();
            ParseAnd(tokens, ref pos, result);

            if (pos < tokens.Count)
                return new ParsedQueryExpression { Error = $"Unexpected token '{tokens[pos]}' at position {pos}" };

            return result;
        }
        catch (Exception ex)
        {
            return new ParsedQueryExpression { Error = ex.Message };
        }
    }

    private static void ParseAnd(List<string> tokens, ref int pos, ParsedQueryExpression result)
    {
        ParseCondition(tokens, ref pos, result);
        while (pos < tokens.Count && tokens[pos].Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            ParseCondition(tokens, ref pos, result);
        }
    }

    private static void ParseCondition(List<string> tokens, ref int pos, ParsedQueryExpression result)
    {
        if (pos >= tokens.Count)
            throw new InvalidOperationException("Unexpected end of expression");

        var field = tokens[pos++];

        if (pos >= tokens.Count)
            throw new InvalidOperationException($"Expected operator after '{field}'");

        var op = tokens[pos++];

        if (pos >= tokens.Count)
            throw new InvalidOperationException($"Expected value after operator '{op}'");

        var rawValue = tokens[pos++];
        var value = rawValue.Trim('"');

        ApplyCondition(field, op, value, result);
    }

    private static void ApplyCondition(string field, string op, string value, ParsedQueryExpression result)
    {
        var fieldLower = field.ToLowerInvariant();
        var opLower = op.ToLowerInvariant();

        if (fieldLower.StartsWith("prop:"))
        {
            var propKey = field[5..];
            var propOp = opLower switch
            {
                "==" or "equals" => PropertyFilterOp.Equals,
                "!=" or "notequals" => PropertyFilterOp.NotEquals,
                "contains" => PropertyFilterOp.Contains,
                _ => throw new InvalidOperationException($"Unsupported operator '{op}' for prop filter. Use ==, !=, or contains.")
            };
            result.PropertyConditions.Add(new PropertyCondition(propKey, value, propOp));
            return;
        }

        switch (fieldLower)
        {
            case "level":
                if (!Enum.TryParse<LogLevel>(value, true, out var level))
                    throw new InvalidOperationException($"Unknown log level '{value}'. Valid values: Verbose, Debug, Information, Warning, Error, Fatal");
                switch (opLower)
                {
                    case ">=" : result.MinLevel = level; break;
                    case ">"  : result.MinLevel = level + 1; break;
                    case "<=" : result.MaxLevel = level; break;
                    case "<"  : result.MaxLevel = level - 1; break;
                    case "==" : result.MinLevel = level; result.MaxLevel = level; break;
                    case "!=" : break; // not directly expressible as range — ignored
                    default: throw new InvalidOperationException($"Unsupported operator '{op}' for level. Use >=, >, <=, <, ==.");
                }
                break;

            case "app":
            case "application":
                if (opLower is not ("==" or "contains"))
                    throw new InvalidOperationException($"Unsupported operator '{op}' for app. Use == or contains.");
                result.SourceApplication = value;
                break;

            case "env":
            case "environment":
                if (opLower is not ("==" or "equals"))
                    throw new InvalidOperationException($"Unsupported operator '{op}' for env. Use ==.");
                result.SourceEnvironment = value;
                break;

            case "message":
            case "msg":
                if (opLower is not ("contains" or "=="))
                    throw new InvalidOperationException($"Unsupported operator '{op}' for message. Use contains or ==.");
                result.MessageContains = value;
                break;

            case "exception":
            case "ex":
                if (opLower is not ("contains" or "=="))
                    throw new InvalidOperationException($"Unsupported operator '{op}' for exception. Use contains or ==.");
                result.ExceptionContains = value;
                break;

            case "trace":
            case "traceid":
                if (opLower is not ("==" or "equals"))
                    throw new InvalidOperationException($"Unsupported operator '{op}' for trace. Use ==.");
                result.TraceId = value;
                break;

            case "timestamp":
            case "time":
            case "ts":
                if (!DateTimeOffset.TryParse(value, out var ts))
                    throw new InvalidOperationException($"Cannot parse timestamp '{value}'. Use ISO 8601 format, e.g. \"2024-01-15T00:00:00Z\".");
                switch (opLower)
                {
                    case ">=" : result.From = ts; break;
                    case ">"  : result.From = ts.AddSeconds(1); break;
                    case "<=" : result.To = ts; break;
                    case "<"  : result.To = ts.AddSeconds(-1); break;
                    case "==" :
                        result.From = ts;
                        result.To = ts.AddSeconds(1).AddTicks(-1);
                        break;
                    default: throw new InvalidOperationException($"Unsupported operator '{op}' for timestamp. Use >=, >, <=, <.");
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown field '{field}'. Valid fields: level, app, env, message, exception, trace, timestamp, prop:<Key>");
        }
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var i = 0;
        expression = expression.Trim();

        while (i < expression.Length)
        {
            if (char.IsWhiteSpace(expression[i])) { i++; continue; }

            // Quoted string
            if (expression[i] == '"')
            {
                var j = i + 1;
                while (j < expression.Length && expression[j] != '"') j++;
                tokens.Add(expression[i..(j + 1)]);
                i = j + 1;
                continue;
            }

            // Multi-char operators: >=, <=, !=
            if (i + 1 < expression.Length && expression[i + 1] == '=')
            {
                if (expression[i] is '>' or '<' or '!' or '=')
                {
                    tokens.Add(expression[i..(i + 2)]);
                    i += 2;
                    continue;
                }
            }

            // Single-char operator >  <
            if (expression[i] is '>' or '<')
            {
                tokens.Add(expression[i].ToString());
                i++;
                continue;
            }

            // Word (field name, keyword, unquoted value)
            var start = i;
            while (i < expression.Length && !char.IsWhiteSpace(expression[i]) && expression[i] != '"')
                i++;
            tokens.Add(expression[start..i]);
        }

        return tokens;
    }
}

public class ParsedQueryExpression
{
    public LogLevel? MinLevel { get; set; }
    public LogLevel? MaxLevel { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceEnvironment { get; set; }
    public string? MessageContains { get; set; }
    public string? ExceptionContains { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public List<PropertyCondition> PropertyConditions { get; } = new();
    public string? Error { get; set; }

    public bool HasError => Error is not null;
}
