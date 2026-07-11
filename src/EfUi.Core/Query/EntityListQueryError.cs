namespace EfUi.Core.Query;

public sealed record EntityListQueryError(
    string Code,
    string Message,
    string? Field = null);
