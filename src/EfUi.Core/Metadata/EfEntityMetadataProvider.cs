using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Metadata;

public sealed class EfEntityMetadataProvider : IEntityMetadataProvider
{
    public EntityDiscoveryResult GetDiscoveryResult(DbContext dbContext)
    {
        var issues = new List<EntityDiscoveryIssue>();
        var entities = new List<EntityMetadata>();

        foreach (var entityType in dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.IsClass)
            .Where(entityType => !IsSharedJoinEntity(entityType)))
        {
            var entity = Build(entityType, issues);
            if (entity is not null)
            {
                entities.Add(entity);
            }
        }

        return new EntityDiscoveryResult(
            entities.OrderBy(entity => entity.RouteName).ToList(),
            issues);
    }

    public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext)
        => GetDiscoveryResult(dbContext).Entities;

    public EntityMetadata GetEntity(DbContext dbContext, string routeName)
        => GetEntities(dbContext).Single(entity => entity.RouteName == routeName);

    private static EntityMetadata? Build(IEntityType entityType, List<EntityDiscoveryIssue> issues)
    {
        var routeName = GetRouteName(entityType);
        if (!TryGetPrimaryKeyProperty(entityType, routeName, issues, out var keyProperty))
        {
            return null;
        }

        var scalarProperties = BuildScalarProperties(entityType, keyProperty);
        var referenceFields = BuildReferenceFields(entityType, routeName, scalarProperties, issues, out var relatedPropertyMap);
        scalarProperties = ApplyRelatedPropertyMap(scalarProperties, relatedPropertyMap);

        var suppressedScalarPropertyNames = referenceFields
            .Select(field => field.Property.Name)
            .ToHashSet(StringComparer.Ordinal);

        var collectionFields = BuildManyToManyCollectionFields(entityType, routeName, issues);
        var (oneToManyFields, relatedManagementLinks) = BuildOneToManyFields(entityType);

        var primaryKeyMetadata = scalarProperties.Single(property => property.IsPrimaryKey);
        var editableProperties = scalarProperties.Where(property => property.IsEditableOnUpdate).ToList();
        var createEditableFields = BuildCreateEditableFields(scalarProperties, referenceFields, suppressedScalarPropertyNames);
        var updateEditableFields = BuildUpdateEditableFields(editableProperties, referenceFields, suppressedScalarPropertyNames, collectionFields, oneToManyFields);

        return new EntityMetadata(
            entityType.ClrType.Name,
            routeName,
            entityType.ClrType,
            primaryKeyMetadata,
            scalarProperties,
            editableProperties,
            createEditableFields,
            updateEditableFields,
            relatedManagementLinks);
    }

    private static bool TryGetPrimaryKeyProperty(IEntityType entityType, string routeName, List<EntityDiscoveryIssue> issues, out IProperty keyProperty)
    {
        var keyProperties = entityType.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count != 1)
        {
            var reason = keyProperties is null
                ? "has no primary key"
                : "has a composite primary key";

            issues.Add(new EntityDiscoveryIssue(
                routeName,
                $"Entity '{entityType.ClrType.Name}' {reason} and cannot be rendered yet.",
                CanRender: false));
            keyProperty = null!;
            return false;
        }

        keyProperty = keyProperties[0];
        return true;
    }

    private static IReadOnlyList<EntityPropertyMetadata> BuildScalarProperties(IEntityType entityType, IProperty keyProperty)
        => entityType.GetProperties()
            .Select(property => new EntityPropertyMetadata(
                property.Name,
                property.ClrType,
                IsEditableOnCreate(property),
                IsEditableOnUpdate(property),
                property.Name == keyProperty.Name))
            .ToList();

    private static List<ReferenceFieldMetadata> BuildReferenceFields(IEntityType entityType, string routeName, IReadOnlyList<EntityPropertyMetadata> scalarProperties, List<EntityDiscoveryIssue> issues, out Dictionary<string, EntityPropertyMetadata> relatedPropertyMap)
    {
        var referenceFields = new List<ReferenceFieldMetadata>();
        relatedPropertyMap = new Dictionary<string, EntityPropertyMetadata>(StringComparer.Ordinal);

        foreach (var foreignKey in entityType.GetForeignKeys())
        {
            if (foreignKey.Properties.Count != 1)
            {
                issues.Add(new EntityDiscoveryIssue(
                    routeName,
                    $"Foreign key '{DescribeForeignKey(foreignKey)}' is composite and is not supported yet.",
                    CanRender: true));
                continue;
            }

            var foreignKeyProperty = scalarProperties.Single(property => property.Name == foreignKey.Properties[0].Name);
            var relatedKey = foreignKey.PrincipalEntityType.FindPrimaryKey()?.Properties.SingleOrDefault();
            if (relatedKey is null)
            {
                issues.Add(new EntityDiscoveryIssue(
                    routeName,
                    $"Foreign key '{foreignKeyProperty.Name}' points to '{foreignKey.PrincipalEntityType.ClrType.Name}', but that entity does not have a single primary key.",
                    CanRender: true));
                continue;
            }

            var relatedRouteName = GetRouteName(foreignKey.PrincipalEntityType);
            var relatedDisplayPropertyName = ResolveRelatedDisplayPropertyName(foreignKey)
                ?? ResolveRelatedDisplayPropertyName(foreignKey.PrincipalEntityType);

            if (foreignKey.DependentToPrincipal is null)
            {
                if (foreignKey.Properties[0].IsShadowProperty())
                {
                    issues.Add(new EntityDiscoveryIssue(
                        routeName,
                        $"Foreign key '{foreignKeyProperty.Name}' has no CLR navigation property and cannot use the simple scalar fallback.",
                        CanRender: true));
                    continue;
                }

                relatedPropertyMap[foreignKeyProperty.Name] = foreignKeyProperty with
                {
                    RelatedClrType = foreignKey.PrincipalEntityType.ClrType,
                    RelatedRouteName = relatedRouteName,
                    RelatedDisplayPropertyName = relatedDisplayPropertyName
                };
                continue;
            }

            referenceFields.Add(new ReferenceFieldMetadata(
                foreignKey,
                foreignKeyProperty,
                foreignKey.DependentToPrincipal!.Name,
                foreignKey.PrincipalEntityType.ClrType,
                relatedRouteName,
                relatedDisplayPropertyName));

            relatedPropertyMap[foreignKeyProperty.Name] = foreignKeyProperty with
            {
                RelatedClrType = foreignKey.PrincipalEntityType.ClrType,
                RelatedRouteName = relatedRouteName,
                RelatedDisplayPropertyName = relatedDisplayPropertyName
            };
        }

        return referenceFields;
    }

    private static IReadOnlyList<EntityPropertyMetadata> ApplyRelatedPropertyMap(IReadOnlyList<EntityPropertyMetadata> scalarProperties, IReadOnlyDictionary<string, EntityPropertyMetadata> relatedPropertyMap)
        => scalarProperties
            .Select(property => relatedPropertyMap.TryGetValue(property.Name, out var relatedProperty)
                ? relatedProperty
                : property)
            .ToList();

    private static List<EditableFieldMetadata> BuildManyToManyCollectionFields(IEntityType entityType, string routeName, List<EntityDiscoveryIssue> issues)
    {
        var collectionFields = new List<EditableFieldMetadata>();
        foreach (var navigation in entityType.GetSkipNavigations().Where(navigation => navigation.IsCollection))
        {
            var targetPrimaryKey = navigation.TargetEntityType.FindPrimaryKey()?.Properties;
            if (targetPrimaryKey is null || targetPrimaryKey.Count != 1)
            {
                issues.Add(new EntityDiscoveryIssue(
                    routeName,
                    $"Collection navigation '{navigation.Name}' targets '{navigation.TargetEntityType.ClrType.Name}', which does not have a single primary key.",
                    CanRender: true));
                continue;
            }

            collectionFields.Add(new EditableFieldMetadata(
                navigation.Name,
                EditableFieldKind.Collection,
                typeof(string[]),
                null,
                navigation.Name,
                navigation.TargetEntityType.ClrType,
                false,
                CollectionRelationshipKind.ManyToMany,
                ResolveRelatedDisplayPropertyName(navigation.TargetEntityType)));
        }

        return collectionFields;
    }

    private static (List<EditableFieldMetadata> OneToManyFields, List<RelatedEntityManagementLink> RelatedManagementLinks) BuildOneToManyFields(IEntityType entityType)
    {
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
                    navigation.TargetEntityType.ClrType,
                    classification.ForeignKey!.Properties[0].Name));
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
                CollectionRelationshipKind.OneToMany,
                ResolveRelatedDisplayPropertyName(navigation.TargetEntityType)));
        }

        return (oneToManyFields, relatedManagementLinks);
    }

    private static List<EditableFieldMetadata> BuildCreateEditableFields(IReadOnlyList<EntityPropertyMetadata> scalarProperties, IReadOnlyList<ReferenceFieldMetadata> referenceFields, ISet<string> suppressedScalarPropertyNames)
        => scalarProperties
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
                    !IsNullable(field.Property.ClrType),
                    RelatedDisplayPropertyName: field.RelatedDisplayPropertyName)))
            .ToList();

    private static List<EditableFieldMetadata> BuildUpdateEditableFields(IReadOnlyList<EntityPropertyMetadata> editableProperties, IReadOnlyList<ReferenceFieldMetadata> referenceFields, ISet<string> suppressedScalarPropertyNames, IReadOnlyList<EditableFieldMetadata> collectionFields, IReadOnlyList<EditableFieldMetadata> oneToManyFields)
        => editableProperties
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
                    !IsNullable(field.Property.ClrType),
                    RelatedDisplayPropertyName: field.RelatedDisplayPropertyName)))
            .Concat(collectionFields)
            .Concat(oneToManyFields)
            .ToList();

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
        if (ShouldRenderAsManagementLink(navigation.TargetEntityType))
        {
            return new CollectionNavigationClassification(foreignKey, IsManagementLink: true);
        }

        return new CollectionNavigationClassification(foreignKey, IsManagementLink: false);
    }

    private static bool ShouldRenderAsManagementLink(IEntityType targetEntityType)
        => targetEntityType.GetForeignKeys().Count() == 2;

    private sealed record CollectionNavigationClassification(IForeignKey ForeignKey, bool IsManagementLink);

    private sealed record ReferenceFieldMetadata(
        IForeignKey ForeignKey,
        EntityPropertyMetadata Property,
        string NavigationName,
        Type RelatedClrType,
        string RelatedRouteName,
        string? RelatedDisplayPropertyName);

    private static EditableFieldMetadata CreateScalarField(EntityPropertyMetadata property)
        => new(
            property.Name,
            EditableFieldKind.Scalar,
            property.ClrType,
            property.Name,
            null,
            null,
            !IsNullable(property.ClrType),
            RelatedDisplayPropertyName: property.RelatedDisplayPropertyName);

    private static bool IsSharedJoinEntity(IEntityType entityType)
        => entityType.ClrType == typeof(Dictionary<string, object>)
           && entityType.FindPrimaryKey()?.Properties.Count != 1;

    private static string? ResolveRelatedDisplayPropertyName(IForeignKey foreignKey)
    {
        var navigationAttribute = foreignKey.DependentToPrincipal?.PropertyInfo?.GetCustomAttribute<EfUiDisplayColumnAttribute>();
        if (navigationAttribute is not null)
        {
            return navigationAttribute.PropertyName;
        }

        return foreignKey.PrincipalEntityType.ClrType.GetCustomAttribute<EfUiDisplayColumnAttribute>()?.PropertyName;
    }

    private static string? ResolveRelatedDisplayPropertyName(IEntityType entityType)
        => entityType.ClrType.GetCustomAttribute<EfUiDisplayColumnAttribute>()?.PropertyName;

    private static string DescribeForeignKey(IForeignKey foreignKey)
        => foreignKey.DependentToPrincipal?.Name
           ?? string.Join(", ", foreignKey.Properties.Select(property => property.Name));

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
