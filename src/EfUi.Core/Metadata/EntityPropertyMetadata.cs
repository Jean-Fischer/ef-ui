namespace EfUi.Core.Metadata;

public sealed record EntityPropertyMetadata(
    string Name,
    Type ClrType,
    bool IsEditableOnCreate,
    bool IsEditableOnUpdate,
    bool IsPrimaryKey = false);
