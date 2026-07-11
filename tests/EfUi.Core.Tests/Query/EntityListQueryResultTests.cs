using EfUi.Core.Rendering;
using EfUi.Core.Query;
using FluentAssertions;
using Xunit;

namespace EfUi.Core.Tests.Query;

public class EntityListQueryResultTests
{
    [Fact]
    public void Result_represents_projected_rows_and_applied_query_state()
    {
        var cell = new EntityListQueryCell("42", "Douglas Adams", "authors");
        var row = new EntityListQueryRow(
            "7",
            new Dictionary<string, EntityListQueryCell>(StringComparer.Ordinal)
            {
                ["Author"] = cell
            });
        var result = new EntityListQueryResult(
            [row],
            [new TableFilterClause("Author", "contains", "Douglas")],
            [new TableSortClause("Author", "asc")],
            [new EntityListQueryError("invalid-filter", "The filter is invalid.", "Author")],
            ["Some related values could not be loaded."],
            Offset: 20,
            Limit: 10);

        result.Rows.Should().ContainSingle().Which.Should().Be(row);
        result.Rows[0].Key.Should().Be("7");
        result.Rows[0].Cells["Author"].RawValue.Should().Be("42");
        result.Rows[0].Cells["Author"].DisplayText.Should().Be("Douglas Adams");
        result.Rows[0].Cells["Author"].RelatedRouteName.Should().Be("authors");
        result.AppliedFilters.Should().ContainSingle().Which.Should().Be(new TableFilterClause("Author", "contains", "Douglas"));
        result.AppliedSorts.Should().ContainSingle().Which.Should().Be(new TableSortClause("Author", "asc"));
        result.Errors.Should().ContainSingle().Which.Should().Be(new EntityListQueryError("invalid-filter", "The filter is invalid.", "Author"));
        result.Warnings.Should().ContainSingle().Which.Should().Be("Some related values could not be loaded.");
        result.Offset.Should().Be(20);
        result.Limit.Should().Be(10);
    }

    [Fact]
    public void Result_defaults_optional_collections_and_supports_query_scoped_errors_without_total_count()
    {
        var result = new EntityListQueryResult(
            [new EntityListQueryRow("1", new Dictionary<string, EntityListQueryCell>()),],
            errors: [new EntityListQueryError("query-failed", "The query could not be completed.")]);

        result.AppliedFilters.Should().BeEmpty();
        result.AppliedSorts.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Errors.Single().Field.Should().BeNull();
        typeof(EntityListQueryResult).GetProperty("TotalCount").Should().BeNull();
    }
}
