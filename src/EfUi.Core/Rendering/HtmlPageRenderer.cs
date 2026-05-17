using System.Net;
using System.Text;
using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public sealed class HtmlPageRenderer : IHtmlPageRenderer
{
    public string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities)
    {
        var html = new StringBuilder();
        html.Append("<html><body><h1>EF UI</h1><ul>");

        foreach (var entity in entities)
        {
            html.Append($"<li><a href=\"{routePrefix}/{entity.RouteName}\">{WebUtility.HtmlEncode(entity.DisplayName)}</a></li>");
        }

        html.Append("</ul></body></html>");
        return html.ToString();
    }

    public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors)
    {
        var html = new StringBuilder();
        html.Append("<html><body>");
        html.Append($"<form method=\"post\" action=\"{routePrefix}/{entity.RouteName}\">");

        foreach (var property in entity.EditableProperties)
        {
            html.Append($"<label>{WebUtility.HtmlEncode(property.Name)}</label>");
            html.Append($"<input name=\"{property.Name}\" />");
        }

        html.Append("<button type=\"submit\">Save</button></form>");
        html.Append("</body></html>");
        return html.ToString();
    }
}
