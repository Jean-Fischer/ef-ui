using EfUi.Core.Rendering;

namespace EfUi.Core.Query;

public sealed record EntityListQueryResult
{
    public EntityListQueryResult(
        IReadOnlyList<EntityListQueryRow> rows,
        IReadOnlyList<TableFilterClause>? appliedFilters = null,
        IReadOnlyList<TableSortClause>? appliedSorts = null,
        IReadOnlyList<EntityListQueryError>? errors = null,
        IReadOnlyList<string>? warnings = null,
        int offset = 0,
        int limit = 50)
    {
        Rows = rows;
        AppliedFilters = appliedFilters ?? [];
        AppliedSorts = appliedSorts ?? [];
        Errors = errors ?? [];
        Warnings = warnings ?? [];
        Offset = offset;
        Limit = limit;
    }

    public IReadOnlyList<EntityListQueryRow> Rows { get; init; }
    public IReadOnlyList<TableFilterClause> AppliedFilters { get; init; }
    public IReadOnlyList<TableSortClause> AppliedSorts { get; init; }
    public IReadOnlyList<EntityListQueryError> Errors { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
}
