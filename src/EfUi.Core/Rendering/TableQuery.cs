namespace EfUi.Core.Rendering;

public sealed record TableQuery(
    IReadOnlyList<TableFilterClause>? filters = null,
    IReadOnlyList<TableSortClause>? sorts = null,
    int Offset = 0,
    int Limit = 50)
{
    public IReadOnlyList<TableFilterClause> Filters { get; init; } = filters ?? [];
    public IReadOnlyList<TableSortClause> Sorts { get; init; } = sorts ?? [];
}

public sealed record TableFilterClause(string Field, string Operator, string? Value);

public sealed record TableSortClause(string Field, string Direction);
