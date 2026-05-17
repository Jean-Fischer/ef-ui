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

    public string RenderList(string routePrefix, EntityMetadata entity, IReadOnlyList<object> rows)
    {
        var html = new StringBuilder();
        html.Append("<html><body>");
        html.Append($"<h1>{WebUtility.HtmlEncode(entity.DisplayName)}</h1>");
        html.Append($"<a href=\"{routePrefix}/{entity.RouteName}/new\">Create New</a>");
        html.Append("<table><thead><tr>");

        foreach (var property in entity.AllProperties)
        {
            html.Append($"<th>{WebUtility.HtmlEncode(property.Name)}</th>");
        }

        html.Append("<th>Actions</th></tr></thead><tbody>");

        foreach (var row in rows)
        {
            html.Append("<tr>");

            foreach (var property in entity.AllProperties)
            {
                var value = row.GetType().GetProperty(property.Name)?.GetValue(row);
                html.Append($"<td>{WebUtility.HtmlEncode(FormatValue(value))}</td>");
            }

            var key = row.GetType().GetProperty(entity.PrimaryKeyProperty.Name)?.GetValue(row);
            var escapedKey = EscapeRouteSegment(key);
            html.Append("<td>");
            html.Append($"<a href=\"{routePrefix}/{entity.RouteName}/{escapedKey}/edit\">Edit</a>");
            html.Append($"<form method=\"post\" action=\"{routePrefix}/{entity.RouteName}/{escapedKey}/delete\" style=\"display:inline\">");
            html.Append("<button type=\"submit\">Delete</button></form>");
            html.Append("</td></tr>");
        }

        html.Append("</tbody></table></body></html>");
        return html.ToString();
    }

    public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, IReadOnlyDictionary<string, string?>? submittedValues = null)
        => RenderEditForm(routePrefix, entity, model, isCreate, errors, null, submittedValues);

    public string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key, IReadOnlyDictionary<string, string?>? submittedValues = null)
    {
        var action = isCreate
            ? $"{routePrefix}/{entity.RouteName}"
            : $"{routePrefix}/{entity.RouteName}/{EscapeRouteSegment(key)}";

        var html = new StringBuilder();
        html.Append("<html><body>");
        html.Append($"<form method=\"post\" action=\"{action}\">");

        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                html.Append($"<div>{WebUtility.HtmlEncode(message)}</div>");
            }
        }

        var editableProperties = isCreate ? entity.CreateEditableProperties : entity.UpdateEditableProperties;

        foreach (var property in editableProperties)
        {
            string value;
            if (submittedValues is not null && submittedValues.TryGetValue(property.Name, out var submittedValue))
            {
                value = submittedValue ?? string.Empty;
            }
            else
            {
                value = model is null
                    ? string.Empty
                    : FormatValue(model.GetType().GetProperty(property.Name)?.GetValue(model));
            }

            html.Append($"<label>{WebUtility.HtmlEncode(property.Name)}</label>");
            html.Append($"<input name=\"{property.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" />");
        }

        html.Append("<button type=\"submit\">Save</button></form>");
        html.Append("</body></html>");
        return html.ToString();
    }

    private static string EscapeRouteSegment(object? value)
        => Uri.EscapeDataString(FormatValue(value));

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
    }
}
