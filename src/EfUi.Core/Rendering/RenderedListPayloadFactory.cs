using System.Text.Json;
using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public static class RenderedListPayloadFactory
{
    public static object Create(string routePrefix, EntityMetadata entity, RenderedListView view)
    {
        var activeFilters = view.Filters
            .GroupBy(filter => filter.Field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var activeSorts = view.Sorts
            .GroupBy(sort => sort.Field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        return new
        {
            library = "tabulator",
            entity = entity.RouteName,
            listUrl = $"{routePrefix}/{entity.RouteName}",
            dataUrl = $"{routePrefix}/{entity.RouteName}/data",
            columns = entity.AllProperties
                .Select(property =>
                {
                    activeFilters.TryGetValue(property.Name, out var activeFilter);
                    activeSorts.TryGetValue(property.Name, out var activeSort);
                    return new
                    {
                        field = property.Name,
                        title = property.Name,
                        headerSort = true,
                        headerFilter = (object)"input",
                        filterOperator = (string?)(activeFilter?.Operator ?? GetDefaultFilterOperator(property)),
                        activeFilterOperator = activeFilter?.Operator,
                        headerFilterValue = activeFilter?.Value,
                        sortDirection = activeSort?.Direction,
                        isFilterable = true
                    };
                })
                .Concat(new[]
                {
                    new
                    {
                        field = "__actions",
                        title = "Actions",
                        headerSort = false,
                        headerFilter = (object)false,
                        filterOperator = (string?)null,
                        activeFilterOperator = (string?)null,
                        headerFilterValue = (string?)null,
                        sortDirection = (string?)null,
                        isFilterable = false
                    }
                })
                .ToList(),
            rows = view.Rows.Select(row =>
            {
                var values = row.Cells.ToDictionary(
                    cell => cell.Key,
                    cell => (object?)new { text = cell.Value.Text, href = cell.Value.Href },
                    StringComparer.Ordinal);
                values["__actions"] = BuildRowActionsMarkup(routePrefix, entity, row.Key);
                return values;
            }).ToList(),
            query = new
            {
                filters = view.Filters,
                sorts = view.Sorts,
                offset = view.Offset,
                limit = view.Limit
            },
            status = new
            {
                items = BuildStatusItems(view),
                errors = view.Errors,
                emptyMessage = view.Filters.Count == 0 && view.Sorts.Count == 0
                    ? "No active filters or sorts"
                    : null,
                offset = view.Offset,
                limit = view.Limit
            }
        };
    }

    public static string Serialize(string routePrefix, EntityMetadata entity, RenderedListView view)
        => JsonSerializer.Serialize(Create(routePrefix, entity, view))
            .Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildStatusItems(RenderedListView view)
    {
        var items = new List<string>();
        items.AddRange(view.Filters.Select(filter => $"{filter.Field} {filter.Operator} {filter.Value ?? string.Empty}"));
        items.AddRange(view.Sorts.Select(sort => $"{sort.Field} {sort.Direction}"));
        return items;
    }

    private static string GetDefaultFilterOperator(EntityPropertyMetadata property)
        => property.RelatedRouteName is not null || property.ClrType == typeof(string)
            ? "contains"
            : "eq";

    private static string BuildRowActionsMarkup(string routePrefix, EntityMetadata entity, string rowKey)
    {
        var escapedKey = Uri.EscapeDataString(rowKey);
        return $"<a class=\"efui-row-action-link\" href=\"{routePrefix}/{entity.RouteName}/{escapedKey}/edit\">Edit</a><form class=\"efui-row-action-form\" method=\"post\" action=\"{routePrefix}/{entity.RouteName}/{escapedKey}/delete\"><button class=\"efui-row-action-button\" type=\"submit\">Delete</button></form>";
    }
}
