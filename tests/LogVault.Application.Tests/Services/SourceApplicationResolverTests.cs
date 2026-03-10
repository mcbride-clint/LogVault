using FluentAssertions;
using LogVault.Application.Services;
using Xunit;

namespace LogVault.Application.Tests.Services;

public class SourceApplicationResolverTests
{
    private readonly SourceApplicationResolver _resolver = new();

    [Fact]
    public void Resolve_ExplicitOverrideTakesPriority()
    {
        var result = _resolver.Resolve("prop", "header", "key", "override");
        result.Should().Be("override");
    }

    [Fact]
    public void Resolve_PropertyValueSecond()
    {
        var result = _resolver.Resolve("prop", "header", "key", null);
        result.Should().Be("prop");
    }

    [Fact]
    public void Resolve_HeaderValueThird()
    {
        var result = _resolver.Resolve(null, "header", "key", null);
        result.Should().Be("header");
    }

    [Fact]
    public void Resolve_ApiKeyDefaultFourth()
    {
        var result = _resolver.Resolve(null, null, "key", null);
        result.Should().Be("key");
    }

    [Fact]
    public void Resolve_AllNull_ReturnsUnknown()
    {
        var result = _resolver.Resolve(null, null, null, null);
        result.Should().Be("Unknown");
    }

    [Fact]
    public void Resolve_EmptyStrings_FallsThrough()
    {
        var result = _resolver.Resolve("", "  ", "", null);
        result.Should().Be("Unknown");
    }
}
