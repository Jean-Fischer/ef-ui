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
        var applyResult = await ApplyValuesAsync(dbContext, instance, entity.CreateEditableFields, values);
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

        var applyResult = await ApplyValuesAsync(dbContext, instance, entity.UpdateEditableFields, values);
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

    private async Task<CrudOperationResult> ApplyValuesAsync(DbContext dbContext, object instance, IReadOnlyList<EditableFieldMetadata> fields, IReadOnlyDictionary<string, string[]> values)
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
            var collectionResult = await ApplyCollectionFieldAsync(dbContext, instance, collectionField.Field, collectionField.RawValues);
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

    private async Task<CrudOperationResult> ApplyCollectionFieldAsync(DbContext dbContext, object instance, EditableFieldMetadata field, IReadOnlyList<string> rawValues)
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

    private static IReadOnlyDictionary<string, string[]> ToMultiValueDictionary(IReadOnlyDictionary<string, string?> values)
        => values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? Array.Empty<string>() : [pair.Value],
            StringComparer.OrdinalIgnoreCase);
}
