namespace EfUi.Core.Rendering;

public sealed record RenderedListRow(
    string Key,
    IReadOnlyDictionary<string, string> Cells);
