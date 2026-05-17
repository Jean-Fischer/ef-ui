using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EfUi.AspNetCore;

public static class EfUiApplicationBuilderExtensions
{
    public static WebApplication UseEfUi(this WebApplication app, Action<EfUiOptions> configure)
    {
        var options = new EfUiOptions();
        configure(options);

        if (!options.EnableInProduction && app.Environment.IsProduction())
        {
            return app;
        }

        app.MapGet(options.RoutePrefix, (IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var renderer = new HtmlPageRenderer();

            var entities = entityMetadataProvider.GetEntities(dbContext);
            var html = renderer.RenderIndex(options.RoutePrefix, entities);

            return Results.Content(html, "text/html");
        });

        app.MapGet($"{options.RoutePrefix}/{{entity}}", (string entity, IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var renderer = new HtmlPageRenderer();

            var metadata = entityMetadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);
            if (metadata is null)
            {
                return Results.NotFound();
            }

            var rows = ReadRows(dbContext, metadata.ClrType);
            var html = renderer.RenderList(options.RoutePrefix, metadata, rows);
            return Results.Content(html, "text/html");
        });

        app.MapGet($"{options.RoutePrefix}/{{entity}}/new", (string entity, IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var renderer = new HtmlPageRenderer();

            var metadata = entityMetadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);
            if (metadata is null)
            {
                return Results.NotFound();
            }

            var html = renderer.RenderEditForm(options.RoutePrefix, metadata, null, true, new Dictionary<string, string[]>(), null);
            return Results.Content(html, "text/html");
        });

        app.MapGet($"{options.RoutePrefix}/{{entity}}/{{id}}/edit", async (string entity, string id, IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var renderer = new HtmlPageRenderer();

            var metadata = entityMetadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);
            if (metadata is null)
            {
                return Results.NotFound();
            }

            var key = TryReadKey(dbContext, metadata, id);
            if (key is null)
            {
                return Results.NotFound();
            }

            var model = await dbContext.FindAsync(metadata.ClrType, key);
            if (model is null)
            {
                return Results.NotFound();
            }

            var html = renderer.RenderEditForm(options.RoutePrefix, metadata, model, false, new Dictionary<string, string[]>(), key);
            return Results.Content(html, "text/html");
        });

        app.MapPost($"{options.RoutePrefix}/{{entity}}", async (string entity, HttpRequest request, IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var crudService = CreateCrudService();
            var values = await ReadFormAsync(request);
            var result = await crudService.CreateAsync(dbContext, entity, values);

            return result.IsSuccess
                ? Results.Redirect($"{options.RoutePrefix}/{entity}")
                : CreateFailureResult(options.RoutePrefix, dbContext, entity, result, null, isCreate: true, submittedValues: values);
        });

        app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}", async (string entity, string id, HttpRequest request, IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var metadata = entityMetadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);
            if (metadata is null)
            {
                return Results.NotFound();
            }

            var key = TryReadKey(dbContext, metadata, id);
            if (key is null)
            {
                return Results.NotFound();
            }

            var crudService = CreateCrudService();
            var values = await ReadFormAsync(request);
            var result = await crudService.UpdateAsync(dbContext, entity, key, values);

            return result.IsSuccess
                ? Results.Redirect($"{options.RoutePrefix}/{entity}")
                : CreateFailureResult(options.RoutePrefix, dbContext, entity, result, key, isCreate: false, submittedValues: values);
        });

        app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}/delete", async (string entity, string id, IServiceProvider services) =>
        {
            var dbContext = ResolveDbContext(services, options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var renderer = new HtmlPageRenderer();
            var metadata = entityMetadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);
            if (metadata is null)
            {
                return Results.NotFound();
            }

            var key = TryReadKey(dbContext, metadata, id);
            if (key is null)
            {
                return Results.NotFound();
            }

            var crudService = CreateCrudService();
            var result = await crudService.DeleteAsync(dbContext, entity, key);
            if (!result.IsSuccess)
            {
                return Results.NotFound();
            }

            var html = renderer.RenderList(options.RoutePrefix, metadata, ReadRows(dbContext, metadata.ClrType));
            return Results.Content(html, "text/html");
        });

        return app;
    }

    private static DbContext ResolveDbContext(IServiceProvider services, Type dbContextType)
        => (DbContext)services.GetRequiredService(dbContextType);

    private static EntityCrudService CreateCrudService()
        => new(new EfEntityMetadataProvider(), new ScalarValueBinder());

    private static IReadOnlyList<object> ReadRows(DbContext dbContext, Type entityClrType)
    {
        var setMethod = typeof(DbContext).GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set)
                              && method.IsGenericMethodDefinition
                              && method.GetParameters().Length == 0);

        var queryable = (System.Collections.IEnumerable)setMethod.MakeGenericMethod(entityClrType).Invoke(dbContext, null)!;
        return queryable.Cast<object>().ToList();
    }

    private static object? TryReadKey(DbContext dbContext, EntityMetadata metadata, string id)
    {
        if (dbContext.Model.FindEntityType(metadata.ClrType)?.FindPrimaryKey() is null)
        {
            return null;
        }

        var bindResult = new ScalarValueBinder().Bind(metadata.PrimaryKeyProperty.ClrType, id);
        return bindResult.IsSuccess ? bindResult.Value : null;
    }

    private static IResult CreateFailureResult(string routePrefix, DbContext dbContext, string entity, CrudOperationResult result, object? key, bool isCreate, IReadOnlyDictionary<string, string?> submittedValues)
    {
        if (result.Errors.ContainsKey("entity") || result.Errors.ContainsKey("id"))
        {
            return Results.NotFound();
        }

        var entityMetadataProvider = new EfEntityMetadataProvider();
        var renderer = new HtmlPageRenderer();
        var metadata = entityMetadataProvider.GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var model = !isCreate && key is not null ? dbContext.Find(metadata.ClrType, key) : null;
        var html = renderer.RenderEditForm(routePrefix, metadata, model, isCreate, result.Errors, key, submittedValues);
        return Results.Content(html, "text/html", statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<Dictionary<string, string?>> ReadFormAsync(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        return form.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
