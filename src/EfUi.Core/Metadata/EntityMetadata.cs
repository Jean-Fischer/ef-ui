namespace EfUi.Core.Metadata;

public sealed record EntityMetadata
{
    public EntityMetadata(
        string displayName,
        string routeName,
        Type clrType,
        EntityPropertyMetadata primaryKeyProperty,
        IReadOnlyList<EntityPropertyMetadata> allProperties,
        IReadOnlyList<EntityPropertyMetadata> editableProperties)
    {
        DisplayName = displayName;
        RouteName = routeName;
        ClrType = clrType;
        PrimaryKeyProperty = primaryKeyProperty;
        AllProperties = allProperties;
        EditableProperties = editableProperties;
        CreateEditableProperties = allProperties.Where(property => property.IsEditableOnCreate).ToList();
        UpdateEditableProperties = editableProperties;
    }

    public string DisplayName { get; }
    public string RouteName { get; }
    public Type ClrType { get; }
    public EntityPropertyMetadata PrimaryKeyProperty { get; }
    public IReadOnlyList<EntityPropertyMetadata> AllProperties { get; }
    public IReadOnlyList<EntityPropertyMetadata> EditableProperties { get; }
    public IReadOnlyList<EntityPropertyMetadata> CreateEditableProperties { get; }
    public IReadOnlyList<EntityPropertyMetadata> UpdateEditableProperties { get; }
}
