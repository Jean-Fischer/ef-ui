using EfUi.Core.Binding;
using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public sealed class EntityCrudService(IEntityMetadataProvider metadataProvider, IScalarValueBinder binder) : IEntityCrudService
{
    public async Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values)
    {
        var entity = ResolveEntity(dbContext, entityRoute, out var failure);
        if (entity is null)
        {
            return failure!;
        }

        var instance = Activator.CreateInstance(entity.ClrType)!;
        var applyResult = ApplyValues(instance, entity.CreateEditableProperties, values);
        if (!applyResult.IsSuccess)
        {
            return applyResult;
        }

        dbContext.Add(instance);
        return await SaveChangesAsync(dbContext);
    }

    public async Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values)
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

        var applyResult = ApplyValues(instance, entity.UpdateEditableProperties, values);
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

    private CrudOperationResult ApplyValues(object instance, IReadOnlyList<EntityPropertyMetadata> properties, IReadOnlyDictionary<string, string?> values)
    {
        var boundValues = new List<(string PropertyName, object? Value)>();

        foreach (var property in properties)
        {
            if (!values.TryGetValue(property.Name, out var rawValue))
            {
                continue;
            }

            var bindResult = binder.Bind(property.ClrType, rawValue);
            if (!bindResult.IsSuccess)
            {
                return CrudOperationResult.Failure(property.Name, bindResult.Error ?? "Invalid value.");
            }

            boundValues.Add((property.Name, bindResult.Value));
        }

        foreach (var boundValue in boundValues)
        {
            instance.GetType().GetProperty(boundValue.PropertyName)!.SetValue(instance, boundValue.Value);
        }

        return CrudOperationResult.Success();
    }
}
