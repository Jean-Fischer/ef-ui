using EfUi.Core.Rendering;

namespace EfUi.Core.Query;

public sealed record EntityListQueryResult(
    IReadOnlyList<EntityListQueryRow> rows,
    IReadOnlyList<TableFilterClause>? appliedFilters = null,
    IReadOnlyList<TableSortClause>? appliedSorts = null,
    IReadOnlyList<EntityListQueryError>? errors = null,
    IReadOnlyList<string>? warnings = null,
    int Offset = 0,
    int Limit = 50)
{
    public IReadOnlyList<EntityListQueryRow> Rows { get; init; } = rows;
    public IReadOnlyList<TableFilterClause> AppliedFilters { get; init; } = appliedFilters ?? [];
    public IReadOnlyList<TableSortClause> AppliedSorts { get; init; } = appliedSorts ?? [];
    public IReadOnlyList<EntityListQueryError> Errors { get; init; } = errors ?? [];
    public IReadOnlyList<string> Warnings { get; init; } = warnings ?? [];
}
