using FluentAssertions;
using LogVault.Application.Parsing;
using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using Xunit;

namespace LogVault.Application.Tests.Parsing;

public class LogQueryExpressionParserTests
{
    // ── Empty / whitespace ──────────────────────────────────────────────────

    [Fact]
    public void Empty_expression_returns_empty_result()
    {
        var result = LogQueryExpressionParser.Parse("");
        result.HasError.Should().BeFalse();
        result.MinLevel.Should().BeNull();
        result.MaxLevel.Should().BeNull();
        result.SourceApplication.Should().BeNull();
        result.PropertyConditions.Should().BeEmpty();
    }

    [Fact]
    public void Whitespace_expression_returns_empty_result()
    {
        var result = LogQueryExpressionParser.Parse("   ");
        result.HasError.Should().BeFalse();
    }

    // ── Level ───────────────────────────────────────────────────────────────

    [Fact]
    public void Level_gte_sets_MinLevel()
    {
        var result = LogQueryExpressionParser.Parse("level >= Warning");
        result.HasError.Should().BeFalse();
        result.MinLevel.Should().Be(LogLevel.Warning);
        result.MaxLevel.Should().BeNull();
    }

    [Fact]
    public void Level_equals_sets_both_MinLevel_and_MaxLevel()
    {
        var result = LogQueryExpressionParser.Parse("level == Error");
        result.HasError.Should().BeFalse();
        result.MinLevel.Should().Be(LogLevel.Error);
        result.MaxLevel.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void Level_lte_sets_MaxLevel()
    {
        var result = LogQueryExpressionParser.Parse("level <= Information");
        result.HasError.Should().BeFalse();
        result.MaxLevel.Should().Be(LogLevel.Information);
        result.MinLevel.Should().BeNull();
    }

    [Theory]
    [InlineData("level >= Blah")]
    [InlineData("level == UNKNOWN")]
    public void Invalid_level_value_returns_error(string expr)
    {
        var result = LogQueryExpressionParser.Parse(expr);
        result.HasError.Should().BeTrue();
        result.Error.Should().Contain("log level");
    }

    // ── App ─────────────────────────────────────────────────────────────────

    [Fact]
    public void App_equals_sets_SourceApplication()
    {
        var result = LogQueryExpressionParser.Parse("app == \"PaymentService\"");
        result.HasError.Should().BeFalse();
        result.SourceApplication.Should().Be("PaymentService");
    }

    [Fact]
    public void Application_keyword_is_alias_for_app()
    {
        var result = LogQueryExpressionParser.Parse("application == \"MyApp\"");
        result.HasError.Should().BeFalse();
        result.SourceApplication.Should().Be("MyApp");
    }

    // ── Message ─────────────────────────────────────────────────────────────

    [Fact]
    public void Message_contains_sets_MessageContains()
    {
        var result = LogQueryExpressionParser.Parse("message contains \"timeout\"");
        result.HasError.Should().BeFalse();
        result.MessageContains.Should().Be("timeout");
    }

    // ── Exception ───────────────────────────────────────────────────────────

    [Fact]
    public void Exception_contains_sets_ExceptionContains()
    {
        var result = LogQueryExpressionParser.Parse("exception contains \"NullReferenceException\"");
        result.HasError.Should().BeFalse();
        result.ExceptionContains.Should().Be("NullReferenceException");
    }

    // ── TraceId ─────────────────────────────────────────────────────────────

    [Fact]
    public void Trace_equals_sets_TraceId()
    {
        var result = LogQueryExpressionParser.Parse("trace == \"abc123\"");
        result.HasError.Should().BeFalse();
        result.TraceId.Should().Be("abc123");
    }

    [Fact]
    public void Traceid_keyword_is_alias()
    {
        var result = LogQueryExpressionParser.Parse("traceid == \"xyz\"");
        result.HasError.Should().BeFalse();
        result.TraceId.Should().Be("xyz");
    }

    // ── Timestamp ───────────────────────────────────────────────────────────

    [Fact]
    public void Timestamp_gte_sets_From()
    {
        var result = LogQueryExpressionParser.Parse("timestamp >= \"2024-01-01T00:00:00Z\"");
        result.HasError.Should().BeFalse();
        result.From.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Timestamp_lte_sets_To()
    {
        var result = LogQueryExpressionParser.Parse("timestamp <= \"2024-12-31T23:59:59Z\"");
        result.HasError.Should().BeFalse();
        result.To.Should().Be(new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero));
    }

    [Fact]
    public void Invalid_timestamp_returns_error()
    {
        var result = LogQueryExpressionParser.Parse("timestamp >= \"not-a-date\"");
        result.HasError.Should().BeTrue();
        result.Error.Should().Contain("timestamp");
    }

    // ── Properties ──────────────────────────────────────────────────────────

    [Fact]
    public void Prop_equals_adds_PropertyCondition()
    {
        var result = LogQueryExpressionParser.Parse("prop:UserId == \"42\"");
        result.HasError.Should().BeFalse();
        result.PropertyConditions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new PropertyCondition("UserId", "42", PropertyFilterOp.Equals));
    }

    [Fact]
    public void Prop_contains_adds_PropertyCondition_Contains()
    {
        var result = LogQueryExpressionParser.Parse("prop:RequestPath contains \"/api\"");
        result.HasError.Should().BeFalse();
        result.PropertyConditions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new PropertyCondition("RequestPath", "/api", PropertyFilterOp.Contains));
    }

    [Fact]
    public void Prop_notequals_adds_PropertyCondition_NotEquals()
    {
        var result = LogQueryExpressionParser.Parse("prop:Status != \"200\"");
        result.HasError.Should().BeFalse();
        result.PropertyConditions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new PropertyCondition("Status", "200", PropertyFilterOp.NotEquals));
    }

    // ── Compound AND ────────────────────────────────────────────────────────

    [Fact]
    public void Compound_AND_populates_multiple_fields()
    {
        var result = LogQueryExpressionParser.Parse("level >= Error AND app == \"MyApp\" AND message contains \"fail\"");
        result.HasError.Should().BeFalse();
        result.MinLevel.Should().Be(LogLevel.Error);
        result.SourceApplication.Should().Be("MyApp");
        result.MessageContains.Should().Be("fail");
    }

    [Fact]
    public void Multiple_prop_conditions_all_added()
    {
        var result = LogQueryExpressionParser.Parse("prop:UserId == \"42\" AND prop:Environment == \"prod\"");
        result.HasError.Should().BeFalse();
        result.PropertyConditions.Should().HaveCount(2);
        result.PropertyConditions[0].Key.Should().Be("UserId");
        result.PropertyConditions[1].Key.Should().Be("Environment");
    }

    // ── Error cases ─────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_field_returns_error()
    {
        var result = LogQueryExpressionParser.Parse("notafield == \"value\"");
        result.HasError.Should().BeTrue();
        result.Error.Should().Contain("notafield");
    }

    [Fact]
    public void Missing_value_returns_error()
    {
        var result = LogQueryExpressionParser.Parse("level >=");
        result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Unsupported_operator_for_prop_returns_error()
    {
        var result = LogQueryExpressionParser.Parse("prop:Key >= \"value\"");
        result.HasError.Should().BeTrue();
        result.Error.Should().Contain(">=");
    }
}
