using EfUi.Core.Rendering;
using FluentAssertions;
using Xunit;

namespace EfUi.Core.Tests.Rendering;

public class EntityDisplayLabelResolverTests
{
    [Fact]
    public void Resolve_prefers_name_over_other_candidates()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 7,
            Name = "Ada Lovelace",
            Title = "Countess",
            Email = "ada@example.com"
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithDisplayProperties.Id));

        label.Should().Be("Ada Lovelace");
    }

    [Fact]
    public void Resolve_falls_back_to_title()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 7,
            Name = null,
            Title = "Countess",
            Email = "ada@example.com"
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithDisplayProperties.Id));

        label.Should().Be("Countess");
    }

    [Fact]
    public void Resolve_falls_back_to_email()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 7,
            Name = null,
            Title = string.Empty,
            Email = "ada@example.com"
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithDisplayProperties.Id));

        label.Should().Be("ada@example.com");
    }

    [Fact]
    public void Resolve_falls_back_to_primary_key_string_when_no_preferred_property_is_usable()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 42,
            Name = null,
            Title = null,
            Email = null
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithDisplayProperties.Id));

        label.Should().Be("42");
    }

    [Fact]
    public void Resolve_ignores_null_empty_and_whitespace_values()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 7,
            Name = string.Empty,
            Title = "   ",
            Email = "ada@example.com"
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithDisplayProperties.Id));

        label.Should().Be("ada@example.com");
    }

    private sealed class RowWithDisplayProperties
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public string? Title { get; init; }

        public string? Email { get; init; }
    }
}
