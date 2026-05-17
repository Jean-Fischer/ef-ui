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
        : this(
            displayName,
            routeName,
            clrType,
            primaryKeyProperty,
            allProperties,
            editableProperties,
            allProperties
                .Where(property => property.IsEditableOnCreate)
                .Select(property => new EditableFieldMetadata(
                    property.Name,
                    EditableFieldKind.Scalar,
                    property.ClrType,
                    property.Name,
                    null,
                    null,
                    false))
                .ToList(),
            editableProperties
                .Select(property => new EditableFieldMetadata(
                    property.Name,
                    EditableFieldKind.Scalar,
                    property.ClrType,
                    property.Name,
                    null,
                    null,
                    false))
                .ToList())
    {
    }

    public EntityMetadata(
        string displayName,
        string routeName,
        Type clrType,
        EntityPropertyMetadata primaryKeyProperty,
        IReadOnlyList<EntityPropertyMetadata> allProperties,
        IReadOnlyList<EntityPropertyMetadata> editableProperties,
        IReadOnlyList<EditableFieldMetadata> createEditableFields,
        IReadOnlyList<EditableFieldMetadata> updateEditableFields)
    {
        DisplayName = displayName;
        RouteName = routeName;
        ClrType = clrType;
        PrimaryKeyProperty = primaryKeyProperty;
        AllProperties = allProperties;
        EditableProperties = editableProperties;
        CreateEditableProperties = allProperties.Where(property => property.IsEditableOnCreate).ToList();
        UpdateEditableProperties = editableProperties;
        CreateEditableFields = createEditableFields;
        UpdateEditableFields = updateEditableFields;
    }

    public string DisplayName { get; }
    public string RouteName { get; }
    public Type ClrType { get; }
    public EntityPropertyMetadata PrimaryKeyProperty { get; }
    public IReadOnlyList<EntityPropertyMetadata> AllProperties { get; }
    public IReadOnlyList<EntityPropertyMetadata> EditableProperties { get; }
    public IReadOnlyList<EntityPropertyMetadata> CreateEditableProperties { get; }
    public IReadOnlyList<EntityPropertyMetadata> UpdateEditableProperties { get; }
    public IReadOnlyList<EditableFieldMetadata> CreateEditableFields { get; }
    public IReadOnlyList<EditableFieldMetadata> UpdateEditableFields { get; }
}
