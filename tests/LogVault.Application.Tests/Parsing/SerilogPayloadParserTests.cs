using FluentAssertions;
using LogVault.Application.Parsing;
using LogVault.Domain.Entities;
using System.Text;
using Xunit;

namespace LogVault.Application.Tests.Parsing;

public class SerilogPayloadParserTests
{
    [Fact]
    public void ParseBatch_StandardFormat_ParsesCorrectly()
    {
        var json = """
            {
                "events": [
                    {
                        "@t": "2024-01-15T10:22:33.000Z",
                        "@mt": "User {UserId} logged in",
                        "@l": "Information",
                        "UserId": 42,
                        "Application": "MyApp"
                    }
                ]
            }
            """;

        var events = SerilogPayloadParser.ParseBatch(json);

        events.Should().HaveCount(1);
        events[0].Level.Should().Be(LogLevel.Information);
        events[0].MessageTemplate.Should().Be("User {UserId} logged in");
        events[0].SourceApplication.Should().Be("MyApp");
        events[0].Timestamp.Should().BeCloseTo(
            new DateTimeOffset(2024, 1, 15, 10, 22, 33, TimeSpan.Zero), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ParseBatch_MissingOptionalFields_DoesNotThrow()
    {
        var json = """{"events": [{"@t": "2024-01-01T00:00:00Z", "@mt": "Hello"}]}""";
        var events = SerilogPayloadParser.ParseBatch(json);
        events.Should().HaveCount(1);
        events[0].Exception.Should().BeNull();
        events[0].SourceApplication.Should().BeNull();
    }

    [Fact]
    public void ParseBatch_MalformedJson_ReturnsEmpty()
    {
        var events = SerilogPayloadParser.ParseBatch("not json");
        events.Should().BeEmpty();
    }

    [Fact]
    public void ParseClef_SingleLine_ParsesCorrectly()
    {
        var clef = """{"@t":"2024-01-01T12:00:00Z","@mt":"Test","@l":"Warning","App":"SvcA"}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(clef));
        var events = SerilogPayloadParser.ParseClef(stream);

        events.Should().HaveCount(1);
        events[0].Level.Should().Be(LogLevel.Warning);
        events[0].MessageTemplate.Should().Be("Test");
    }

    [Fact]
    public void ParseClef_MultipleLines_ParsesAll()
    {
        var clef = """
            {"@t":"2024-01-01T12:00:00Z","@mt":"Event1","@l":"Information"}
            {"@t":"2024-01-01T12:01:00Z","@mt":"Event2","@l":"Error"}
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(clef));
        var events = SerilogPayloadParser.ParseClef(stream);
        events.Should().HaveCount(2);
    }

    [Fact]
    public void ParseClef_SkipsMalformedLines()
    {
        var clef = """
            {"@t":"2024-01-01T12:00:00Z","@mt":"Good"}
            not json at all
            {"@t":"2024-01-01T12:01:00Z","@mt":"AlsoGood"}
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(clef));
        var events = SerilogPayloadParser.ParseClef(stream);
        events.Should().HaveCount(2);
    }
}
