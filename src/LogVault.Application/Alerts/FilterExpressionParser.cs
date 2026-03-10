using LogVault.Domain.Entities;
using System.Text.Json;

namespace LogVault.Application.Alerts;

/// <summary>
/// Parses a simple alert filter expression into a predicate delegate.
/// Supported syntax:
///   level >= Error
///   app == "PaymentService"
///   message contains "timeout"
///   exception contains "SqlException"
///   prop:UserId == "42"
///   expr AND expr
///   expr OR expr
///   (expr)
/// </summary>
public static class FilterExpressionParser
{
    public static Func<LogEvent, bool> Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return _ => true;

        var tokens = Tokenize(expression);
        var pos = 0;
        var predicate = ParseOr(tokens, ref pos);
        return predicate;
    }

    private static Func<LogEvent, bool> ParseOr(List<string> tokens, ref int pos)
    {
        var left = ParseAnd(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseAnd(tokens, ref pos);
            var capturedLeft = left;
            var capturedRight = right;
            left = e => capturedLeft(e) || capturedRight(e);
        }
        return left;
    }

    private static Func<LogEvent, bool> ParseAnd(List<string> tokens, ref int pos)
    {
        var left = ParseAtom(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseAtom(tokens, ref pos);
            var capturedLeft = left;
            var capturedRight = right;
            left = e => capturedLeft(e) && capturedRight(e);
        }
        return left;
    }

    private static Func<LogEvent, bool> ParseAtom(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new InvalidOperationException("Unexpected end of expression");

        if (tokens[pos] == "(")
        {
            pos++; // consume '('
            var inner = ParseOr(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos] != ")")
                throw new InvalidOperationException("Expected ')'");
            pos++; // consume ')'
            return inner;
        }

        var field = tokens[pos++];
        if (pos >= tokens.Count)
            throw new InvalidOperationException($"Expected operator after '{field}'");

        var op = tokens[pos++];
        if (pos >= tokens.Count)
            throw new InvalidOperationException($"Expected value after operator '{op}'");

        var value = tokens[pos++].Trim('"');

        return BuildPredicate(field, op, value);
    }

    private static Func<LogEvent, bool> BuildPredicate(string field, string op, string value)
    {
        if (field.StartsWith("prop:", StringComparison.OrdinalIgnoreCase))
        {
            var propKey = field[5..];
            return op.ToLowerInvariant() switch
            {
                "==" => e => GetPropertyValue(e.PropertiesJson, propKey) == value,
                "!=" => e => GetPropertyValue(e.PropertiesJson, propKey) != value,
                "contains" => e => GetPropertyValue(e.PropertiesJson, propKey)?.Contains(value, StringComparison.OrdinalIgnoreCase) == true,
                _ => throw new InvalidOperationException($"Unsupported operator '{op}' for prop filter")
            };
        }

        return field.ToLowerInvariant() switch
        {
            "level" => BuildLevelPredicate(op, value),
            "app" or "application" => op.ToLowerInvariant() switch
            {
                "==" => e => string.Equals(e.SourceApplication, value, StringComparison.OrdinalIgnoreCase),
                "!=" => e => !string.Equals(e.SourceApplication, value, StringComparison.OrdinalIgnoreCase),
                "contains" => e => e.SourceApplication?.Contains(value, StringComparison.OrdinalIgnoreCase) == true,
                _ => throw new InvalidOperationException($"Unsupported operator '{op}' for app filter")
            },
            "message" => op.ToLowerInvariant() switch
            {
                "contains" => e => e.RenderedMessage.Contains(value, StringComparison.OrdinalIgnoreCase),
                "==" => e => string.Equals(e.RenderedMessage, value, StringComparison.OrdinalIgnoreCase),
                _ => throw new InvalidOperationException($"Unsupported operator '{op}' for message filter")
            },
            "exception" => op.ToLowerInvariant() switch
            {
                "contains" => e => e.Exception?.Contains(value, StringComparison.OrdinalIgnoreCase) == true,
                "==" => e => string.Equals(e.Exception, value, StringComparison.OrdinalIgnoreCase),
                _ => throw new InvalidOperationException($"Unsupported operator '{op}' for exception filter")
            },
            _ => throw new InvalidOperationException($"Unknown field '{field}'")
        };
    }

    private static Func<LogEvent, bool> BuildLevelPredicate(string op, string value)
    {
        if (!Enum.TryParse<Domain.Entities.LogLevel>(value, true, out var targetLevel))
            throw new InvalidOperationException($"Invalid log level '{value}'");

        return op switch
        {
            ">=" => e => e.Level >= targetLevel,
            "<=" => e => e.Level <= targetLevel,
            ">" => e => e.Level > targetLevel,
            "<" => e => e.Level < targetLevel,
            "==" => e => e.Level == targetLevel,
            "!=" => e => e.Level != targetLevel,
            _ => throw new InvalidOperationException($"Unsupported operator '{op}' for level filter")
        };
    }

    private static string? GetPropertyValue(string propertiesJson, string key)
    {
        try
        {
            var doc = JsonDocument.Parse(propertiesJson);
            if (doc.RootElement.TryGetProperty(key, out var prop))
                return prop.ToString();
        }
        catch { /* ignore */ }
        return null;
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var i = 0;
        expression = expression.Trim();

        while (i < expression.Length)
        {
            if (char.IsWhiteSpace(expression[i])) { i++; continue; }

            if (expression[i] == '(') { tokens.Add("("); i++; continue; }
            if (expression[i] == ')') { tokens.Add(")"); i++; continue; }

            // Quoted string
            if (expression[i] == '"')
            {
                var j = i + 1;
                while (j < expression.Length && expression[j] != '"') j++;
                tokens.Add(expression[i..(j + 1)]);
                i = j + 1;
                continue;
            }

            // Operator or word
            var start = i;
            while (i < expression.Length && expression[i] != ' ' && expression[i] != '(' && expression[i] != ')')
                i++;
            tokens.Add(expression[start..i]);
        }

        return tokens;
    }
}
