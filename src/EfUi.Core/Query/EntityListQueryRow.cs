namespace EfUi.Core.Query;

public sealed record EntityListQueryRow(
    string Key,
    IReadOnlyDictionary<string, EntityListQueryCell> Cells);
