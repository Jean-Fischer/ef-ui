namespace EfUi.Core.Rendering;

public sealed record RenderedListView(
    IReadOnlyList<RenderedListRow> Rows,
    IReadOnlyList<RenderedListFilter>? filters = null,
    IReadOnlyList<RenderedListSort>? sorts = null,
    IReadOnlyList<string>? errors = null,
    int Offset = 0,
    int Limit = 50,
    IReadOnlyList<string>? warnings = null)
{
    public IReadOnlyList<RenderedListFilter> Filters { get; init; } = filters ?? [];
    public IReadOnlyList<RenderedListSort> Sorts { get; init; } = sorts ?? [];
    public IReadOnlyList<string> Errors { get; init; } = errors ?? [];
    public IReadOnlyList<string> Warnings { get; init; } = warnings ?? [];
}
