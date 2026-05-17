namespace EfUi.Core.Metadata;

public sealed record EntityMetadata(
    string DisplayName,
    string RouteName,
    Type ClrType,
    EntityPropertyMetadata PrimaryKeyProperty,
    IReadOnlyList<EntityPropertyMetadata> AllProperties,
    IReadOnlyList<EntityPropertyMetadata> EditableProperties)
{
    public IReadOnlyList<EntityPropertyMetadata> CreateEditableProperties => AllProperties.Where(property => property.IsEditableOnCreate).ToList();

    public IReadOnlyList<EntityPropertyMetadata> UpdateEditableProperties => EditableProperties;
}
