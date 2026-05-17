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
            var dbContext = (DbContext)services.GetRequiredService(options.DbContextType);
            var entityMetadataProvider = new EfEntityMetadataProvider();
            var renderer = new HtmlPageRenderer();

            var entities = entityMetadataProvider.GetEntities(dbContext);
            var html = renderer.RenderIndex(options.RoutePrefix, entities);

            return Results.Content(html, "text/html");
        });

        return app;
    }
}
