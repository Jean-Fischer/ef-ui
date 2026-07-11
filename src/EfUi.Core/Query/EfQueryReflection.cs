using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Query;

/// <summary>Provides the small amount of reflection needed at the provider-query seam.</summary>
internal static class EfQueryReflection
{
    public static IQueryable GetEntitySet(DbContext dbContext, Type entityType)
    {
        var method = typeof(DbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == nameof(DbContext.Set)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0);
        return (IQueryable)method.MakeGenericMethod(entityType).Invoke(dbContext, null)!;
    }

    public static async Task<List<object>> ToListAsync(
        IQueryable source,
        Type entityType,
        CancellationToken cancellationToken)
    {
        var method = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 2
                && method.GetParameters()[1].ParameterType == typeof(CancellationToken));
        var task = (Task)method.MakeGenericMethod(entityType).Invoke(null, [source, cancellationToken])!;
        await task.ConfigureAwait(false);
        var value = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return ((IEnumerable)value).Cast<object>().ToList();
    }
}
