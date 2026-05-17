using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public interface IEntityCrudService
{
    Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values);

    Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values);

    Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key);
}
