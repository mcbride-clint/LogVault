using FluentAssertions;
using LogVault.Application.Alerts;
using LogVault.Domain.Entities;
using Xunit;

namespace LogVault.Application.Tests.Alerts;

public class FilterExpressionParserTests
{
    private static LogEvent MakeEvent(
        LogLevel level = LogLevel.Error,
        string? app = "TestApp",
        string renderedMessage = "Something failed",
        string? exception = null) => new()
    {
        Level = level,
        SourceApplication = app,
        RenderedMessage = renderedMessage,
        Exception = exception,
        PropertiesJson = "{\"UserId\":\"42\"}",
        MessageTemplate = renderedMessage,
        IngestedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Parse_LevelGte_MatchesCorrectly()
    {
        var pred = FilterExpressionParser.Parse("level >= Error");
        pred(MakeEvent(LogLevel.Error)).Should().BeTrue();
        pred(MakeEvent(LogLevel.Fatal)).Should().BeTrue();
        pred(MakeEvent(LogLevel.Warning)).Should().BeFalse();
    }

    [Fact]
    public void Parse_AppEquals_MatchesCorrectly()
    {
        var pred = FilterExpressionParser.Parse("app == \"TestApp\"");
        pred(MakeEvent(app: "TestApp")).Should().BeTrue();
        pred(MakeEvent(app: "OtherApp")).Should().BeFalse();
    }

    [Fact]
    public void Parse_MessageContains_MatchesCorrectly()
    {
        var pred = FilterExpressionParser.Parse("message contains \"fail\"");
        pred(MakeEvent(renderedMessage: "Something failed")).Should().BeTrue();
        pred(MakeEvent(renderedMessage: "All good")).Should().BeFalse();
    }

    [Fact]
    public void Parse_AndExpression_BothMustMatch()
    {
        var pred = FilterExpressionParser.Parse("level >= Error AND app == \"TestApp\"");
        pred(MakeEvent(LogLevel.Error, "TestApp")).Should().BeTrue();
        pred(MakeEvent(LogLevel.Error, "OtherApp")).Should().BeFalse();
        pred(MakeEvent(LogLevel.Warning, "TestApp")).Should().BeFalse();
    }

    [Fact]
    public void Parse_OrExpression_EitherCanMatch()
    {
        var pred = FilterExpressionParser.Parse("level == Fatal OR app == \"TestApp\"");
        pred(MakeEvent(LogLevel.Fatal, "OtherApp")).Should().BeTrue();
        pred(MakeEvent(LogLevel.Error, "TestApp")).Should().BeTrue();
        pred(MakeEvent(LogLevel.Error, "OtherApp")).Should().BeFalse();
    }

    [Fact]
    public void Parse_PropertyFilter_MatchesJsonValue()
    {
        var pred = FilterExpressionParser.Parse("prop:UserId == \"42\"");
        pred(MakeEvent()).Should().BeTrue();
        pred(new LogEvent { Level = LogLevel.Error, SourceApplication = "X", RenderedMessage = "msg", MessageTemplate = "msg", PropertiesJson = "{\"UserId\":\"99\"}", IngestedAt = DateTimeOffset.UtcNow }).Should().BeFalse();
    }

    [Fact]
    public void Parse_ExceptionContains_MatchesCorrectly()
    {
        var pred = FilterExpressionParser.Parse("exception contains \"SqlException\"");
        pred(MakeEvent(exception: "System.Data.SqlException: timeout")).Should().BeTrue();
        pred(MakeEvent(exception: null)).Should().BeFalse();
    }

    [Fact]
    public void Parse_EmptyExpression_AlwaysTrue()
    {
        var pred = FilterExpressionParser.Parse("");
        pred(MakeEvent()).Should().BeTrue();
    }

    [Fact]
    public void Parse_InvalidLevel_ThrowsException()
    {
        var act = () => FilterExpressionParser.Parse("level >= NotALevel");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_UnknownField_ThrowsException()
    {
        var act = () => FilterExpressionParser.Parse("unknown == \"foo\"");
        act.Should().Throw<InvalidOperationException>();
    }
}
