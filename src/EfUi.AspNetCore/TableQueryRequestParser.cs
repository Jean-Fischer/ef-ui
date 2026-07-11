using EfUi.Core.Rendering;
using Microsoft.AspNetCore.Http;

namespace EfUi.AspNetCore;

internal sealed record TableQueryRequestParseResult(TableQuery Query, IReadOnlyList<string> Errors);

internal static class TableQueryRequestParser
{
    public static TableQueryRequestParseResult Parse(HttpRequest request)
        => Parse(request.Query);

    public static TableQueryRequestParseResult Parse(IQueryCollection query)
    {
        var errors = new List<string>();
        var filters = ParseFilterClauses(query).ToList();
        var sorts = ParseSortClauses(query, errors).ToList();
        var offset = ReadNonNegativeInt(query, "offset", 0, errors);
        var limit = ReadPositiveInt(query, "limit", 50, errors);

        return new TableQueryRequestParseResult(new TableQuery(filters, sorts, offset, limit), errors);
    }

    private static IEnumerable<TableFilterClause> ParseFilterClauses(IQueryCollection query)
    {
        foreach (var index in GetClauseIndexes(query, "filter"))
        {
            var field = GetValue(query, $"filter.{index}.field");
            var op = GetValue(query, $"filter.{index}.op");
            var value = GetValue(query, $"filter.{index}.value");

            if (string.IsNullOrWhiteSpace(field)
                && string.IsNullOrWhiteSpace(op)
                && string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            yield return new TableFilterClause(field ?? string.Empty, op ?? string.Empty, value);
        }
    }

    private static IEnumerable<TableSortClause> ParseSortClauses(IQueryCollection query, ICollection<string> errors)
    {
        foreach (var index in GetClauseIndexes(query, "sort"))
        {
            var field = GetValue(query, $"sort.{index}.field");
            var direction = GetValue(query, $"sort.{index}.dir");

            if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(direction))
            {
                continue;
            }

            if (!string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Unsupported sort direction '{direction}'.");
                continue;
            }

            yield return new TableSortClause(field ?? string.Empty, direction!);
        }
    }

    private static IEnumerable<int> GetClauseIndexes(IQueryCollection query, string prefix)
        => query.Keys
            .Where(key => key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            .Select(key => key.Split('.', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 3)
            .Select(parts => int.TryParse(parts[1], out var index) ? index : -1)
            .Where(index => index >= 0)
            .Distinct()
            .OrderBy(index => index);

    private static int ReadNonNegativeInt(IQueryCollection query, string key, int fallback, ICollection<string> errors)
    {
        var rawValue = GetValue(query, key);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (int.TryParse(rawValue, out var parsed) && parsed >= 0)
        {
            return parsed;
        }

        errors.Add($"Unsupported {key} value '{rawValue}'.");
        return fallback;
    }

    private static int ReadPositiveInt(IQueryCollection query, string key, int fallback, ICollection<string> errors)
    {
        var rawValue = GetValue(query, key);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (int.TryParse(rawValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        errors.Add($"Unsupported {key} value '{rawValue}'.");
        return fallback;
    }

    private static string? GetValue(IQueryCollection query, string key)
        => query[key].FirstOrDefault();
}
