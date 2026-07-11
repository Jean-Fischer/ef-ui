using EfUi.AspNetCore;
using EfUi.Core.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public sealed class TableQueryRequestParserTests
{
    [Fact]
    public void Parse_reads_filter_and_sort_clauses_in_numeric_index_order()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["sort.10.dir"] = "desc",
            ["filter.7.value"] = "second",
            ["sort.2.field"] = "CreatedAt",
            ["filter.2.op"] = "eq",
            ["sort.10.field"] = "Name",
            ["filter.7.op"] = "contains",
            ["filter.2.field"] = "Email",
            ["filter.7.field"] = "DisplayName",
            ["sort.2.dir"] = "asc"
        });

        var result = TableQueryRequestParser.Parse(query);

        result.Errors.Should().BeEmpty();
        result.Query.Filters.Should().BeEquivalentTo(
            [
                new TableFilterClause("Email", "eq", null),
                new TableFilterClause("DisplayName", "contains", "second")
            ], options => options.WithStrictOrdering());
        result.Query.Sorts.Should().BeEquivalentTo(
            [
                new TableSortClause("CreatedAt", "asc"),
                new TableSortClause("Name", "desc")
            ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void Parser_types_are_internal_implementation_details()
    {
        typeof(TableQueryRequestParser).IsPublic.Should().BeFalse();
        typeof(TableQueryRequestParseResult).IsPublic.Should().BeFalse();
    }

    [Fact]
    public void Parse_uses_defaults_when_offset_and_limit_are_missing()
    {
        var result = TableQueryRequestParser.Parse(new QueryCollection());

        result.Query.Offset.Should().Be(0);
        result.Query.Limit.Should().Be(50);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_preserves_unknown_sort_field_with_valid_direction_for_core_validation()
    {
        var result = TableQueryRequestParser.Parse(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["sort.0.field"] = "NotAVisibleField",
            ["sort.0.dir"] = "asc"
        }));

        result.Errors.Should().BeEmpty();
        result.Query.Sorts.Should().ContainSingle().Which.Should().Be(
            new TableSortClause("NotAVisibleField", "asc"));
    }

    [Fact]
    public void Parse_reads_non_default_offset_and_limit()
    {
        var result = TableQueryRequestParser.Parse(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["offset"] = "25",
            ["limit"] = "10"
        }));

        result.Errors.Should().BeEmpty();
        result.Query.Offset.Should().Be(25);
        result.Query.Limit.Should().Be(10);
    }

    [Theory]
    [InlineData("offset", "not-an-integer")]
    [InlineData("offset", "-1")]
    [InlineData("limit", "not-an-integer")]
    [InlineData("limit", "0")]
    public void Parse_reports_invalid_integer_syntax_and_ranges(string key, string value)
    {
        var result = TableQueryRequestParser.Parse(new QueryCollection(new Dictionary<string, StringValues>
        {
            [key] = value
        }));

        result.Errors.Should().ContainSingle(error => error.Contains($"Unsupported {key} value '{value}'", StringComparison.Ordinal));
        result.Query.Offset.Should().Be(0);
        result.Query.Limit.Should().Be(50);
    }

    [Fact]
    public void Parse_reports_invalid_sort_direction_without_validating_field_or_operator_semantics()
    {
        var result = TableQueryRequestParser.Parse(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["filter.3.field"] = "NotAVisibleField",
            ["filter.3.op"] = "future-operator",
            ["filter.3.value"] = "text with spaces & punctuation",
            ["sort.1.field"] = "NotAVisibleField",
            ["sort.1.dir"] = "sideways"
        }));

        result.Errors.Should().ContainSingle(error => error.Contains("Unsupported sort direction 'sideways'", StringComparison.Ordinal));
        result.Query.Filters.Should().ContainSingle().Which.Should().Be(new TableFilterClause(
            "NotAVisibleField", "future-operator", "text with spaces & punctuation"));
        result.Query.Sorts.Should().BeEmpty();
    }

    [Fact]
    public void Parse_accepts_request_and_preserves_a_missing_optional_filter_value()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?filter.0.field=Name&filter.0.op=contains");

        var result = TableQueryRequestParser.Parse(context.Request);

        result.Errors.Should().BeEmpty();
        result.Query.Filters.Should().ContainSingle().Which.Value.Should().BeNull();
    }
}
