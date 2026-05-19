namespace EfUi.Core.Metadata;

public enum EditableFieldKind
{
    Scalar,
    Reference,
    Collection
}

public sealed record EditableFieldMetadata(
    string Name,
    EditableFieldKind Kind,
    Type ValueType,
    string? ScalarPropertyName,
    string? NavigationPropertyName,
    Type? RelatedClrType,
    bool IsRequired,
    CollectionRelationshipKind CollectionRelationshipKind = CollectionRelationshipKind.None,
    string? RelatedDisplayPropertyName = null);
