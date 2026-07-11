namespace EfUi.Core.Query;

public sealed record EntityListQueryCell(
    string? RawValue,
    string DisplayText,
    string? RelatedRouteName = null);
