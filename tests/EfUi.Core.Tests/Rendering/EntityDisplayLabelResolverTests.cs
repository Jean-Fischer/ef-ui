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
    public void Resolve_uses_title_when_name_is_whitespace()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 7,
            Name = "   ",
            Title = "Countess",
            Email = "ada@example.com"
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithDisplayProperties.Id));

        label.Should().Be("Countess");
    }

    [Fact]
    public void Resolve_uses_email_when_it_is_the_only_populated_preferred_property()
    {
        var row = new RowWithDisplayProperties
        {
            Id = 7,
            Name = null,
            Title = null,
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

    [Fact]
    public void Resolve_uses_non_string_preferred_property_values_via_to_string()
    {
        var row = new RowWithNonStringDisplayProperty
        {
            Id = 7,
            Name = new DisplayValue("Ada Lovelace")
        };

        var label = EntityDisplayLabelResolver.Resolve(row, nameof(RowWithNonStringDisplayProperty.Id));

        label.Should().Be("Ada Lovelace");
    }

    private sealed class RowWithDisplayProperties
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public string? Title { get; init; }

        public string? Email { get; init; }
    }

    private sealed class RowWithNonStringDisplayProperty
    {
        public int Id { get; init; }

        public object? Name { get; init; }
    }

    private sealed class DisplayValue(string value)
    {
        public override string ToString() => value;
    }
}
