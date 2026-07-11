using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public interface IEntityCrudService
{
    Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values);
    Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string[]> values)
        => CreateAsync(dbContext, entityRoute, ToSingleValueDictionary(values));

    Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values);

    Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string[]> values)
        => UpdateAsync(dbContext, entityRoute, key, ToSingleValueDictionary(values));

    Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key);

    private static IReadOnlyDictionary<string, string?> ToSingleValueDictionary(IReadOnlyDictionary<string, string[]> values)
        => values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.FirstOrDefault(),
            StringComparer.OrdinalIgnoreCase);
}
