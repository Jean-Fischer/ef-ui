using EfUi.Core.Metadata;
using EfUi.Core.Query;
using EfUi.Core.Rendering;

namespace EfUi.AspNetCore;

internal static class RenderedListViewAdapter
{
    public static RenderedListView Create(
        string routePrefix,
        EntityMetadata metadata,
        EntityListQueryResult result,
        IReadOnlyList<string>? parserErrors = null,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(routePrefix);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(result);

        var errors = (parserErrors ?? [])
            .Concat(result.Errors.Select(error => error.Message))
            .ToList();
        var combinedWarnings = (warnings ?? [])
            .Concat(result.Warnings)
            .ToList();

        return new RenderedListView(
            result.Rows.Select(row => new RenderedListRow(
                row.Key,
                row.Cells.ToDictionary(
                    pair => pair.Key,
                    pair => ToRenderedCell(routePrefix, metadata, pair.Value),
                    StringComparer.Ordinal))).ToList(),
            result.AppliedFilters
                .Select(filter => new RenderedListFilter(filter.Field, filter.Operator, filter.Value))
                .ToList(),
            result.AppliedSorts
                .Select(sort => new RenderedListSort(sort.Field, sort.Direction))
                .ToList(),
            errors,
            result.Offset,
            result.Limit,
            combinedWarnings);
    }

    private static RenderedListCell ToRenderedCell(string routePrefix, EntityMetadata metadata, EntityListQueryCell cell)
    {
        var href = cell.RelatedRouteName is not null
                   && !string.IsNullOrWhiteSpace(cell.RawValue)
            ? $"{routePrefix}/{cell.RelatedRouteName}/{Uri.EscapeDataString(cell.RawValue)}/edit"
            : null;

        return new RenderedListCell(cell.DisplayText, href);
    }
}
