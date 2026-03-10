using FluentAssertions;
using LogVault.Application.Parsing;
using Xunit;

namespace LogVault.Application.Tests.Parsing;

public class TraceParentParserTests
{
    [Fact]
    public void Parse_ValidHeader_ReturnsTraceIdAndSpanId()
    {
        var header = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var (traceId, spanId) = TraceParentParser.Parse(header);
        traceId.Should().Be("4bf92f3577b34da6a3ce929d0e0e4736");
        spanId.Should().Be("00f067aa0ba902b7");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Parse_NullOrEmpty_ReturnsNulls(string? header)
    {
        var (traceId, spanId) = TraceParentParser.Parse(header);
        traceId.Should().BeNull();
        spanId.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-traceparent")]
    [InlineData("00-short-short-01")]
    [InlineData("00-GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG-00f067aa0ba902b7-01")]
    public void Parse_MalformedHeader_ReturnsNulls(string header)
    {
        var (traceId, spanId) = TraceParentParser.Parse(header);
        traceId.Should().BeNull();
        spanId.Should().BeNull();
    }

    [Fact]
    public void Parse_AllZeroTraceId_ReturnsNulls()
    {
        var header = "00-00000000000000000000000000000000-00f067aa0ba902b7-01";
        var (traceId, spanId) = TraceParentParser.Parse(header);
        traceId.Should().BeNull();
        spanId.Should().BeNull();
    }
}
