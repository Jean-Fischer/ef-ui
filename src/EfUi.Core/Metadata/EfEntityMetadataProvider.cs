using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Metadata;

public sealed class EfEntityMetadataProvider : IEntityMetadataProvider
{
    public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext)
    {
        return dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.IsClass)
            .Where(entityType => !IsSharedJoinEntity(entityType))
            .Select(Build)
            .OrderBy(x => x.RouteName)
            .ToList();
    }

    public EntityMetadata GetEntity(DbContext dbContext, string routeName)
    {
        return GetEntities(dbContext).Single(x => x.RouteName == routeName);
    }

    private static EntityMetadata Build(IEntityType entityType)
    {
        var keyProperties = entityType.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count != 1)
        {
            throw new InvalidOperationException($"Entity '{entityType.ClrType.Name}' must have a single primary key.");
        }

        var keyProperty = keyProperties[0];

        var scalarProperties = entityType.GetProperties()
            .Select(property => new EntityPropertyMetadata(
                property.Name,
                property.ClrType,
                IsEditableOnCreate(property),
                IsEditableOnUpdate(property),
                property.Name == keyProperty.Name))
            .ToList();

        var referenceFields = entityType.GetForeignKeys()
            .Where(foreignKey => foreignKey.DependentToPrincipal is not null)
            .Where(foreignKey => foreignKey.Properties.Count == 1)
            .Select(foreignKey => new
            {
                ForeignKey = foreignKey,
                Property = scalarProperties.Single(property => property.Name == foreignKey.Properties[0].Name),
                NavigationName = foreignKey.DependentToPrincipal!.Name,
                RelatedClrType = foreignKey.PrincipalEntityType.ClrType
            })
            .ToList();

        scalarProperties = scalarProperties
            .Select(property =>
            {
                var referenceField = referenceFields.SingleOrDefault(field => field.Property.Name == property.Name);
                return referenceField is null
                    ? property
                    : property with
                    {
                        RelatedClrType = referenceField.RelatedClrType,
                        RelatedRouteName = GetRouteName(referenceField.ForeignKey.PrincipalEntityType)
                    };
            })
            .ToList();

        var suppressedScalarPropertyNames = referenceFields
            .Select(field => field.Property.Name)
            .ToHashSet(StringComparer.Ordinal);

        var collectionFields = entityType.GetSkipNavigations()
            .Where(navigation => navigation.IsCollection)
            .Where(navigation => navigation.TargetEntityType.FindPrimaryKey()?.Properties.Count == 1)
            .Select(navigation => new EditableFieldMetadata(
                navigation.Name,
                EditableFieldKind.Collection,
                typeof(string[]),
                null,
                navigation.Name,
                navigation.TargetEntityType.ClrType,
                false,
                CollectionRelationshipKind.ManyToMany))
            .ToList();

        var oneToManyFields = new List<EditableFieldMetadata>();
        var relatedManagementLinks = new List<RelatedEntityManagementLink>();

        foreach (var navigation in entityType.GetNavigations().Where(navigation => navigation.IsCollection))
        {
            var classification = ClassifyCollectionNavigation(entityType, navigation);
            if (classification is null)
            {
                continue;
            }

            if (classification.IsManagementLink)
            {
                relatedManagementLinks.Add(new RelatedEntityManagementLink(
                    navigation.Name,
                    GetRouteName(navigation.TargetEntityType),
                    navigation.TargetEntityType.ClrType));
                continue;
            }

            oneToManyFields.Add(new EditableFieldMetadata(
                navigation.Name,
                EditableFieldKind.Collection,
                typeof(string[]),
                classification.ForeignKey!.Properties[0].Name,
                navigation.Name,
                navigation.TargetEntityType.ClrType,
                !IsNullable(classification.ForeignKey.Properties[0].ClrType),
                CollectionRelationshipKind.OneToMany));
        }

        var primaryKeyMetadata = scalarProperties.Single(property => property.IsPrimaryKey);
        var editableProperties = scalarProperties.Where(x => x.IsEditableOnUpdate).ToList();

        var createEditableFields = scalarProperties
            .Where(property => property.IsEditableOnCreate)
            .Where(property => !suppressedScalarPropertyNames.Contains(property.Name))
            .Select(CreateScalarField)
            .Concat(referenceFields
                .Where(field => field.Property.IsEditableOnCreate)
                .Select(field => new EditableFieldMetadata(
                    field.NavigationName,
                    EditableFieldKind.Reference,
                    field.Property.ClrType,
                    field.Property.Name,
                    field.NavigationName,
                    field.RelatedClrType,
                    !IsNullable(field.Property.ClrType))))
            .ToList();

        var updateEditableFields = editableProperties
            .Where(property => !suppressedScalarPropertyNames.Contains(property.Name))
            .Select(CreateScalarField)
            .Concat(referenceFields
                .Where(field => field.Property.IsEditableOnUpdate)
                .Select(field => new EditableFieldMetadata(
                    field.NavigationName,
                    EditableFieldKind.Reference,
                    field.Property.ClrType,
                    field.Property.Name,
                    field.NavigationName,
                    field.RelatedClrType,
                    !IsNullable(field.Property.ClrType))))
            .Concat(collectionFields)
            .Concat(oneToManyFields)
            .ToList();

        return new EntityMetadata(
            entityType.ClrType.Name,
            GetRouteName(entityType),
            entityType.ClrType,
            primaryKeyMetadata,
            scalarProperties,
            editableProperties,
            createEditableFields,
            updateEditableFields,
            relatedManagementLinks);
    }

    private static CollectionNavigationClassification? ClassifyCollectionNavigation(IEntityType entityType, INavigation navigation)
    {
        var matchingForeignKeys = navigation.TargetEntityType.GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType == entityType)
            .Where(foreignKey => foreignKey.Properties.Count == 1)
            .Where(foreignKey => foreignKey.PrincipalToDependent?.Name == navigation.Name)
            .ToList();

        if (matchingForeignKeys.Count != 1)
        {
            return null;
        }

        var foreignKey = matchingForeignKeys[0];
        var hasForeignKeyToDifferentPrincipal = navigation.TargetEntityType.GetForeignKeys()
            .Any(candidate => candidate != foreignKey && candidate.PrincipalEntityType != entityType);

        if (hasForeignKeyToDifferentPrincipal)
        {
            return new CollectionNavigationClassification(foreignKey, IsManagementLink: true);
        }

        return new CollectionNavigationClassification(foreignKey, IsManagementLink: false);
    }

    private sealed record CollectionNavigationClassification(IForeignKey ForeignKey, bool IsManagementLink);

    private static EditableFieldMetadata CreateScalarField(EntityPropertyMetadata property)
        => new(
            property.Name,
            EditableFieldKind.Scalar,
            property.ClrType,
            property.Name,
            null,
            null,
            !IsNullable(property.ClrType));

    private static bool IsSharedJoinEntity(IEntityType entityType)
        => entityType.ClrType == typeof(Dictionary<string, object>)
           && entityType.FindPrimaryKey()?.Properties.Count != 1;

    private static string GetRouteName(IEntityType entityType)
    {
        return (entityType.FindAnnotation("Relational:TableName")?.Value as string
                ?? entityType.ClrType.Name)
            .ToLowerInvariant();
    }

    private static bool IsEditableOnCreate(IProperty property)
    {
        if (!CanUserEdit(property))
        {
            return false;
        }

        return !property.IsPrimaryKey() || property.ValueGenerated == ValueGenerated.Never;
    }

    private static bool IsEditableOnUpdate(IProperty property)
        => !property.IsPrimaryKey()
           && CanUserEdit(property);

    private static bool CanUserEdit(IProperty property)
        => !property.IsShadowProperty()
           && property.PropertyInfo?.SetMethod is not null
           && IsSupportedScalar(property.ClrType);

    private static bool IsSupportedScalar(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;

        return actual.IsEnum
            || actual == typeof(string)
            || actual == typeof(bool)
            || actual == typeof(byte)
            || actual == typeof(short)
            || actual == typeof(int)
            || actual == typeof(long)
            || actual == typeof(float)
            || actual == typeof(double)
            || actual == typeof(decimal)
            || actual == typeof(DateTime)
            || actual == typeof(Guid);
    }

    private static bool IsNullable(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
}
