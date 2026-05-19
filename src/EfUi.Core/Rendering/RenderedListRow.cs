namespace EfUi.Core.Rendering;

public sealed record RenderedListRow(
    string Key,
    IReadOnlyDictionary<string, RenderedListCell> Cells);
