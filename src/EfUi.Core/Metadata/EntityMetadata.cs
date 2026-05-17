namespace EfUi.Core.Metadata;

public sealed record EntityMetadata(
    string DisplayName,
    string RouteName,
    Type ClrType,
    IReadOnlyList<EntityPropertyMetadata> AllProperties,
    IReadOnlyList<EntityPropertyMetadata> EditableProperties);
