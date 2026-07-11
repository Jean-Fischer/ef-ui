using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Query;

/// <summary>Validates a table query against the capabilities exposed by an EF model.</summary>
public sealed class EntityListQueryValidator
{
    public EntityListQueryValidationResult Validate(
        DbContext dbContext,
        EntityMetadata metadata,
        TableQuery query)
        => Validate(dbContext.Model, metadata, query);

    public EntityListQueryValidationResult Validate(
        IModel model,
        EntityMetadata metadata,
        TableQuery query)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(query);

        var capabilities = EntityListQueryCapabilities.Create(model, metadata);
        var filters = new List<TableFilterClause>();
        var sorts = new List<TableSortClause>();
        var errors = new List<EntityListQueryError>();

        foreach (var filter in query.Filters)
        {
            if (ValidateFilter(filter, capabilities, errors))
            {
                filters.Add(filter);
            }
        }

        foreach (var sort in query.Sorts)
        {
            if (ValidateSort(sort, capabilities, errors))
            {
                sorts.Add(sort);
            }
        }

        return new EntityListQueryValidationResult(filters, sorts, errors);
    }

    private static bool ValidateFilter(
        TableFilterClause filter,
        EntityListQueryCapabilities capabilities,
        ICollection<EntityListQueryError> errors)
    {
        if (string.IsNullOrWhiteSpace(filter.Field)
            || !capabilities.Fields.TryGetValue(filter.Field, out var field))
        {
            errors.Add(new EntityListQueryError(
                "unsupported-filter-field",
                $"Unsupported filter field '{filter.Field}'.",
                filter.Field));
            return false;
        }

        if (field.IsDisplayOnly)
        {
            errors.Add(new EntityListQueryError(
                "field-display-only",
                $"Field '{filter.Field}' is display-only and cannot be filtered by the provider.",
                filter.Field));
            return false;
        }

        if (!field.IsFilterable)
        {
            errors.Add(new EntityListQueryError(
                "unsupported-filter-field",
                $"Unsupported filter field '{filter.Field}'.",
                filter.Field));
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter.Operator)
            || !field.SupportedOperators.Contains(filter.Operator, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new EntityListQueryError(
                "unsupported-filter-operator",
                $"Unsupported filter operator '{filter.Operator}' for field '{filter.Field}'.",
                filter.Field));
            return false;
        }

        return true;
    }

    private static bool ValidateSort(
        TableSortClause sort,
        EntityListQueryCapabilities capabilities,
        ICollection<EntityListQueryError> errors)
    {
        if (string.IsNullOrWhiteSpace(sort.Field)
            || !capabilities.Fields.TryGetValue(sort.Field, out var field))
        {
            errors.Add(new EntityListQueryError(
                "unsupported-sort-field",
                $"Unsupported sort field '{sort.Field}'.",
                sort.Field));
            return false;
        }

        if (field.IsDisplayOnly)
        {
            errors.Add(new EntityListQueryError(
                "field-display-only",
                $"Field '{sort.Field}' is display-only and cannot be sorted by the provider.",
                sort.Field));
            return false;
        }

        if (!field.IsSortable)
        {
            errors.Add(new EntityListQueryError(
                "unsupported-sort-field",
                $"Unsupported sort field '{sort.Field}'.",
                sort.Field));
            return false;
        }

        if (!string.Equals(sort.Direction, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new EntityListQueryError(
                "unsupported-sort-direction",
                $"Unsupported sort direction '{sort.Direction}' for field '{sort.Field}'.",
                sort.Field));
            return false;
        }

        return true;
    }
}

public sealed class EntityListQueryValidationResult
{
    public EntityListQueryValidationResult(
        IReadOnlyList<TableFilterClause> appliedFilters,
        IReadOnlyList<TableSortClause> appliedSorts,
        IReadOnlyList<EntityListQueryError> errors)
    {
        AppliedFilters = appliedFilters;
        AppliedSorts = appliedSorts;
        Errors = errors;
    }

    public IReadOnlyList<TableFilterClause> AppliedFilters { get; }
    public IReadOnlyList<TableSortClause> AppliedSorts { get; }
    public IReadOnlyList<EntityListQueryError> Errors { get; }
}
