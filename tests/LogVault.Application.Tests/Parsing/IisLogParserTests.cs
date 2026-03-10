using FluentAssertions;
using LogVault.Application.Parsing;
using LogVault.Domain.Entities;
using System.Text;
using Xunit;

namespace LogVault.Application.Tests.Parsing;

public class IisLogParserTests
{
    private const string SampleLog = """
        #Software: Microsoft Internet Information Services 10.0
        #Version: 1.0
        #Date: 2024-01-15 10:00:00
        #Fields: date time s-ip cs-method cs-uri-stem cs-uri-query sc-status time-taken
        2024-01-15 10:00:01 192.168.1.1 GET /api/health - 200 5
        2024-01-15 10:00:02 192.168.1.1 POST /api/data - 404 10
        2024-01-15 10:00:03 192.168.1.1 GET /error - 500 200
        """;

    [Fact]
    public void Parse_StandardIisLog_ParsesAllDataRows()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleLog));
        var events = IisLogParser.Parse(stream);
        events.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_HttpStatus200_IsInformationLevel()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleLog));
        var events = IisLogParser.Parse(stream);
        events[0].Level.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void Parse_HttpStatus404_IsWarningLevel()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleLog));
        var events = IisLogParser.Parse(stream);
        events[1].Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Parse_HttpStatus500_IsErrorLevel()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleLog));
        var events = IisLogParser.Parse(stream);
        events[2].Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void Parse_SkipsCommentLines()
    {
        var logWithComments = "#comment\n#Fields: date time cs-method sc-status time-taken\n2024-01-01 12:00:00 GET 200 5\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(logWithComments));
        var events = IisLogParser.Parse(stream);
        events.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_RenderedMessageContainsMethodAndStatus()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleLog));
        var events = IisLogParser.Parse(stream);
        events[0].RenderedMessage.Should().Contain("GET");
        events[0].RenderedMessage.Should().Contain("200");
    }
}
