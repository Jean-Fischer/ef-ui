using EfUi.Core.Binding;
using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public sealed class EntityCrudService(IEntityMetadataProvider metadataProvider, IScalarValueBinder binder) : IEntityCrudService
{
    public Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values)
        => CreateAsync(dbContext, entityRoute, ToMultiValueDictionary(values));

    public async Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string[]> values)
    {
        var entity = ResolveEntity(dbContext, entityRoute, out var failure);
        if (entity is null)
        {
            return failure!;
        }

        var instance = Activator.CreateInstance(entity.ClrType)!;
        var applyResult = await ApplyValuesAsync(dbContext, entity, instance, entity.CreateEditableFields, values);
        if (!applyResult.IsSuccess)
        {
            return applyResult;
        }

        dbContext.Add(instance);
        return await SaveChangesAsync(dbContext);
    }

    public Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values)
        => UpdateAsync(dbContext, entityRoute, key, ToMultiValueDictionary(values));

    public async Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string[]> values)
    {
        var entity = ResolveEntity(dbContext, entityRoute, out var failure);
        if (entity is null)
        {
            return failure!;
        }

        var instance = await dbContext.FindAsync(entity.ClrType, key);
        if (instance is null)
        {
            return CrudOperationResult.Failure("id", "Row not found.");
        }

        await LoadCollectionFieldsAsync(dbContext, instance, entity.UpdateEditableFields);

        var applyResult = await ApplyValuesAsync(dbContext, entity, instance, entity.UpdateEditableFields, values);
        if (!applyResult.IsSuccess)
        {
            return applyResult;
        }

        return await SaveChangesAsync(dbContext);
    }

    public async Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key)
    {
        var entity = ResolveEntity(dbContext, entityRoute, out var failure);
        if (entity is null)
        {
            return failure!;
        }

        var instance = await dbContext.FindAsync(entity.ClrType, key);
        if (instance is null)
        {
            return CrudOperationResult.Failure("id", "Row not found.");
        }

        dbContext.Remove(instance);
        return await SaveChangesAsync(dbContext);
    }

    private EntityMetadata? ResolveEntity(DbContext dbContext, string entityRoute, out CrudOperationResult? failure)
    {
        var entity = metadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entityRoute);
        if (entity is null)
        {
            failure = CrudOperationResult.Failure("entity", $"Unknown entity '{entityRoute}'.");
            return null;
        }

        failure = null;
        return entity;
    }

    private static async Task<CrudOperationResult> SaveChangesAsync(DbContext dbContext)
    {
        try
        {
            await dbContext.SaveChangesAsync();
            return CrudOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return CrudOperationResult.Failure("persistence", "Could not save changes.");
        }
    }

    private async Task<CrudOperationResult> ApplyValuesAsync(DbContext dbContext, EntityMetadata entity, object instance, IReadOnlyList<EditableFieldMetadata> fields, IReadOnlyDictionary<string, string[]> values)
    {
        var boundValues = new List<(string PropertyName, object? Value)>();
        var collectionFields = new List<(EditableFieldMetadata Field, IReadOnlyList<string> RawValues)>();

        foreach (var field in fields)
        {
            if (!values.TryGetValue(field.Name, out var rawValues))
            {
                continue;
            }

            if (field.Kind == EditableFieldKind.Collection)
            {
                collectionFields.Add((field, rawValues));
                continue;
            }

            var rawValue = rawValues.FirstOrDefault();
            var propertyName = field.ScalarPropertyName ?? field.Name;
            var propertyInfo = instance.GetType().GetProperty(propertyName)!;
            var bindResult = binder.Bind(propertyInfo.PropertyType, rawValue);
            if (!bindResult.IsSuccess)
            {
                return CrudOperationResult.Failure(field.Name, bindResult.Error ?? "Invalid value.");
            }

            boundValues.Add((propertyName, bindResult.Value));
        }

        foreach (var boundValue in boundValues)
        {
            instance.GetType().GetProperty(boundValue.PropertyName)!.SetValue(instance, boundValue.Value);
        }

        foreach (var collectionField in collectionFields)
        {
            var collectionResult = await ApplyCollectionFieldAsync(dbContext, entity, instance, collectionField.Field, collectionField.RawValues);
            if (!collectionResult.IsSuccess)
            {
                return collectionResult;
            }
        }

        return CrudOperationResult.Success();
    }

    private static async Task LoadCollectionFieldsAsync(DbContext dbContext, object instance, IReadOnlyList<EditableFieldMetadata> fields)
    {
        foreach (var field in fields.Where(field => field.Kind == EditableFieldKind.Collection && field.NavigationPropertyName is not null))
        {
            await dbContext.Entry(instance).Collection(field.NavigationPropertyName!).LoadAsync();
        }
    }

    private async Task<CrudOperationResult> ApplyCollectionFieldAsync(DbContext dbContext, EntityMetadata entity, object instance, EditableFieldMetadata field, IReadOnlyList<string> rawValues)
    {
        return field.CollectionRelationshipKind switch
        {
            CollectionRelationshipKind.OneToMany => await ApplyOneToManyCollectionFieldAsync(dbContext, entity, instance, field, rawValues),
            _ => await ApplyManyToManyCollectionFieldAsync(dbContext, instance, field, rawValues)
        };
    }

    private async Task<CrudOperationResult> ApplyManyToManyCollectionFieldAsync(DbContext dbContext, object instance, EditableFieldMetadata field, IReadOnlyList<string> rawValues)
    {
        if (field.NavigationPropertyName is null || field.RelatedClrType is null)
        {
            return CrudOperationResult.Failure(field.Name, "Invalid collection configuration.");
        }

        var entityType = dbContext.Model.FindEntityType(field.RelatedClrType);
        var keyProperty = entityType?.FindPrimaryKey()?.Properties.SingleOrDefault();
        if (keyProperty is null)
        {
            return CrudOperationResult.Failure(field.Name, "Related entity must have a single primary key.");
        }

        var relatedEntities = new List<object>();
        foreach (var selectedValue in rawValues.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var bindResult = binder.Bind(keyProperty.ClrType, selectedValue);
            if (!bindResult.IsSuccess)
            {
                return CrudOperationResult.Failure(field.Name, bindResult.Error ?? "Invalid value.");
            }

            var relatedEntity = await dbContext.FindAsync(field.RelatedClrType, bindResult.Value!);
            if (relatedEntity is null)
            {
                return CrudOperationResult.Failure(field.Name, "Selected related row not found.");
            }

            relatedEntities.Add(relatedEntity);
        }

        var navigationProperty = instance.GetType().GetProperty(field.NavigationPropertyName)!;
        var collection = navigationProperty.GetValue(instance) as System.Collections.IList;
        if (collection is null)
        {
            return CrudOperationResult.Failure(field.Name, "Collection navigation is not editable.");
        }

        collection.Clear();
        foreach (var relatedEntity in relatedEntities)
        {
            collection.Add(relatedEntity);
        }

        return CrudOperationResult.Success();
    }

    private async Task<CrudOperationResult> ApplyOneToManyCollectionFieldAsync(DbContext dbContext, EntityMetadata entity, object instance, EditableFieldMetadata field, IReadOnlyList<string> rawValues)
    {
        if (!TryGetOneToManyCollectionContext(dbContext, field, out var childEntityType, out var childKeyProperty, out var childForeignKeyProperty, out var failure))
        {
            return failure;
        }

        if (!TryBindSelectedChildKeys(rawValues, childKeyProperty.ClrType, field.Name, out var selectedChildKeys, out failure))
        {
            return failure;
        }

        var parentKeyValue = instance.GetType().GetProperty(entity.PrimaryKeyProperty.Name)?.GetValue(instance);
        var childrenByKey = BuildChildrenByKey(dbContext, childEntityType.ClrType, childKeyProperty.Name);

        if (!TryValidateSelectedChildren(childrenByKey, selectedChildKeys, childForeignKeyProperty.Name, parentKeyValue, field.Name, out failure))
        {
            return failure;
        }

        var removedChildren = GetRemovedChildren(instance, field.NavigationPropertyName!, childKeyProperty.Name, selectedChildKeys).ToList();
        if (field.IsRequired && removedChildren.Count != 0)
        {
            return CrudOperationResult.Failure(field.Name, "Required related rows cannot be removed without reassignment.");
        }

        ApplyChildAssignments(childrenByKey, childForeignKeyProperty.Name, parentKeyValue, selectedChildKeys, removedChildren);
        return CrudOperationResult.Success();
    }

    private static bool TryGetOneToManyCollectionContext(DbContext dbContext, EditableFieldMetadata field, out Microsoft.EntityFrameworkCore.Metadata.IEntityType childEntityType, out Microsoft.EntityFrameworkCore.Metadata.IProperty childKeyProperty, out Microsoft.EntityFrameworkCore.Metadata.IProperty childForeignKeyProperty, out CrudOperationResult failure)
    {
        if (field.NavigationPropertyName is null || field.RelatedClrType is null || field.ScalarPropertyName is null)
        {
            childEntityType = null!;
            childKeyProperty = null!;
            childForeignKeyProperty = null!;
            failure = CrudOperationResult.Failure(field.Name, "Invalid collection configuration.");
            return false;
        }

        var resolvedChildEntityType = dbContext.Model.FindEntityType(field.RelatedClrType);
        var resolvedChildKeyProperty = resolvedChildEntityType?.FindPrimaryKey()?.Properties.SingleOrDefault();
        var resolvedChildForeignKeyProperty = resolvedChildEntityType?.FindProperty(field.ScalarPropertyName);
        if (resolvedChildEntityType is null || resolvedChildKeyProperty is null || resolvedChildForeignKeyProperty is null)
        {
            childEntityType = null!;
            childKeyProperty = null!;
            childForeignKeyProperty = null!;
            failure = CrudOperationResult.Failure(field.Name, "Related entity must have a single primary key.");
            return false;
        }

        childEntityType = resolvedChildEntityType;
        childKeyProperty = resolvedChildKeyProperty!;
        childForeignKeyProperty = resolvedChildForeignKeyProperty!;
        failure = CrudOperationResult.Success();
        return true;
    }

    private bool TryBindSelectedChildKeys(IReadOnlyList<string> rawValues, Type childKeyType, string fieldName, out HashSet<string> selectedChildKeys, out CrudOperationResult failure)
    {
        selectedChildKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var selectedValue in rawValues.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var bindResult = binder.Bind(childKeyType, selectedValue);
            if (!bindResult.IsSuccess)
            {
                failure = CrudOperationResult.Failure(fieldName, bindResult.Error ?? "Invalid value.");
                return false;
            }

            selectedChildKeys.Add(FormatValue(bindResult.Value));
        }

        failure = CrudOperationResult.Success();
        return true;
    }

    private static IReadOnlyDictionary<string, object> BuildChildrenByKey(DbContext dbContext, Type childEntityClrType, string childKeyPropertyName)
        => ReadRows(dbContext, childEntityClrType).ToDictionary(
            child => FormatValue(child.GetType().GetProperty(childKeyPropertyName)?.GetValue(child)),
            child => child,
            StringComparer.Ordinal);

    private static bool TryValidateSelectedChildren(IReadOnlyDictionary<string, object> childrenByKey, HashSet<string> selectedChildKeys, string childForeignKeyPropertyName, object? parentKeyValue, string fieldName, out CrudOperationResult failure)
    {
        foreach (var selectedChildKey in selectedChildKeys)
        {
            if (!childrenByKey.TryGetValue(selectedChildKey, out var selectedChild))
            {
                failure = CrudOperationResult.Failure(fieldName, "Selected related row not found.");
                return false;
            }

            var ownerValue = selectedChild.GetType().GetProperty(childForeignKeyPropertyName)?.GetValue(selectedChild);
            if (ownerValue is not null && !Equals(ownerValue, parentKeyValue))
            {
                failure = CrudOperationResult.Failure(fieldName, "Selected related row is already assigned to another parent.");
                return false;
            }
        }

        failure = CrudOperationResult.Success();
        return true;
    }

    private static IEnumerable<object> GetRemovedChildren(object instance, string navigationPropertyName, string childKeyPropertyName, HashSet<string> selectedChildKeys)
        => GetCurrentChildren(instance, navigationPropertyName)
            .Where(child => !selectedChildKeys.Contains(FormatValue(child.GetType().GetProperty(childKeyPropertyName)?.GetValue(child))));

    private static void ApplyChildAssignments(IReadOnlyDictionary<string, object> childrenByKey, string childForeignKeyPropertyName, object? parentKeyValue, HashSet<string> selectedChildKeys, IReadOnlyList<object> removedChildren)
    {
        foreach (var selectedChildKey in selectedChildKeys)
        {
            var selectedChild = childrenByKey[selectedChildKey];
            selectedChild.GetType().GetProperty(childForeignKeyPropertyName)?.SetValue(selectedChild, parentKeyValue);
        }

        foreach (var removedChild in removedChildren)
        {
            removedChild.GetType().GetProperty(childForeignKeyPropertyName)?.SetValue(removedChild, null);
        }
    }

    private static IReadOnlyList<object> ReadRows(DbContext dbContext, Type entityClrType)
    {
        var setMethod = typeof(DbContext).GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set)
                              && method.IsGenericMethodDefinition
                              && method.GetParameters().Length == 0);

        var queryable = (System.Collections.IEnumerable)setMethod.MakeGenericMethod(entityClrType).Invoke(dbContext, null)!;
        return queryable.Cast<object>().ToList();
    }

    private static IEnumerable<object> GetCurrentChildren(object instance, string navigationPropertyName)
    {
        var navigationProperty = instance.GetType().GetProperty(navigationPropertyName);
        var collection = navigationProperty?.GetValue(instance) as System.Collections.IEnumerable;
        return collection?.Cast<object>() ?? [];
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };

    private static IReadOnlyDictionary<string, string[]> ToMultiValueDictionary(IReadOnlyDictionary<string, string?> values)
        => values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? Array.Empty<string>() : [pair.Value],
            StringComparer.OrdinalIgnoreCase);
}
