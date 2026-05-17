using EfUi.Core.Binding;
using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public sealed class EntityCrudService(IEntityMetadataProvider metadataProvider, IScalarValueBinder binder) : IEntityCrudService
{
    public async Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values)
    {
        var entity = metadataProvider.GetEntity(dbContext, entityRoute);
        var instance = Activator.CreateInstance(entity.ClrType)!;
        var applyResult = ApplyValues(entity, instance, values);
        if (!applyResult.IsSuccess)
        {
            return applyResult;
        }

        dbContext.Add(instance);
        await dbContext.SaveChangesAsync();
        return CrudOperationResult.Success();
    }

    public async Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values)
    {
        var entity = metadataProvider.GetEntity(dbContext, entityRoute);
        var instance = await dbContext.FindAsync(entity.ClrType, key);
        if (instance is null)
        {
            return CrudOperationResult.Failure("id", "Row not found.");
        }

        var applyResult = ApplyValues(entity, instance, values);
        if (!applyResult.IsSuccess)
        {
            return applyResult;
        }

        await dbContext.SaveChangesAsync();
        return CrudOperationResult.Success();
    }

    public async Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key)
    {
        var entity = metadataProvider.GetEntity(dbContext, entityRoute);
        var instance = await dbContext.FindAsync(entity.ClrType, key);
        if (instance is null)
        {
            return CrudOperationResult.Failure("id", "Row not found.");
        }

        dbContext.Remove(instance);
        await dbContext.SaveChangesAsync();
        return CrudOperationResult.Success();
    }

    private CrudOperationResult ApplyValues(EntityMetadata entity, object instance, IReadOnlyDictionary<string, string?> values)
    {
        foreach (var property in entity.EditableProperties)
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

            instance.GetType().GetProperty(property.Name)!.SetValue(instance, bindResult.Value);
        }

        return CrudOperationResult.Success();
    }
}
