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

    public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null)
        => RenderEditForm(routePrefix, entity, model, isCreate, errors, null, submittedValues, fieldOptions);

    public string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null)
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

        if (!isCreate)
        {
            var keyValue = model is null
                ? FormatValue(key)
                : FormatValue(model.GetType().GetProperty(entity.PrimaryKeyProperty.Name)?.GetValue(model));

            html.Append($"<div><label>{WebUtility.HtmlEncode(entity.PrimaryKeyProperty.Name)}</label><span>{WebUtility.HtmlEncode(keyValue)}</span></div>");
        }

        var editableFields = isCreate ? entity.CreateEditableFields : entity.UpdateEditableFields;

        foreach (var field in editableFields)
        {
            html.Append($"<label>{WebUtility.HtmlEncode(field.Name)}</label>");

            switch (field.Kind)
            {
                case EditableFieldKind.Reference:
                    RenderReferenceField(html, field, model, submittedValues, fieldOptions);
                    break;
                case EditableFieldKind.Collection:
                    RenderCollectionField(html, field, fieldOptions);
                    break;
                default:
                    RenderScalarField(html, field, model, submittedValues);
                    break;
            }
        }

        if (editableFields.Any(field => field.Kind == EditableFieldKind.Collection))
        {
            RenderCollectionPickerScript(html);
        }

        if (!isCreate && entity.RelatedManagementLinks.Any())
        {
            RenderRelatedManagementLinks(html, routePrefix, entity);
        }

        html.Append("<button type=\"submit\">Save</button></form>");
        html.Append("</body></html>");
        return html.ToString();
    }

    private static void RenderScalarField(StringBuilder html, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues)
    {
        var propertyName = field.ScalarPropertyName ?? field.Name;
        string value;
        if (submittedValues is not null && submittedValues.TryGetValue(field.Name, out var submittedValue))
        {
            value = submittedValue.FirstOrDefault() ?? string.Empty;
        }
        else
        {
            value = model is null
                ? string.Empty
                : FormatValue(model.GetType().GetProperty(propertyName)?.GetValue(model));
        }

        html.Append($"<input name=\"{field.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" />");
    }

    private static void RenderReferenceField(StringBuilder html, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions)
    {
        var currentValue = submittedValues is not null && submittedValues.TryGetValue(field.Name, out var submittedValue)
            ? submittedValue.FirstOrDefault() ?? string.Empty
            : model is null
                ? string.Empty
                : FormatValue(model.GetType().GetProperty(field.ScalarPropertyName!)?.GetValue(model));

        html.Append($"<select name=\"{field.Name}\">");
        html.Append("<option value=\"\"></option>");

        if (fieldOptions is not null && fieldOptions.TryGetValue(field.Name, out var options))
        {
            foreach (var option in options)
            {
                var selected = option.Selected || string.Equals(option.Value, currentValue, StringComparison.Ordinal)
                    ? " selected"
                    : string.Empty;
                html.Append($"<option value=\"{WebUtility.HtmlEncode(option.Value)}\"{selected}>{WebUtility.HtmlEncode(option.Label)}</option>");
            }
        }

        html.Append("</select>");
    }

    private static void RenderCollectionField(StringBuilder html, EditableFieldMetadata field, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions)
    {
        var fieldName = WebUtility.HtmlEncode(field.Name);
        html.Append($"<div class=\"efui-collection-picker\" data-field-name=\"{fieldName}\">");
        html.Append($"<input type=\"search\" class=\"efui-collection-picker-search\" data-role=\"collection-filter\" data-target-field=\"{fieldName}\" placeholder=\"Filter {fieldName}...\" />");
        html.Append("<div class=\"efui-collection-picker-options\" style=\"max-height: 12rem; overflow-y: auto; border: 1px solid #ccc; padding: 0.5rem;\">");

        if (fieldOptions is not null && fieldOptions.TryGetValue(field.Name, out var options))
        {
            foreach (var option in options)
            {
                var selected = option.Selected ? " checked" : string.Empty;
                var disabled = option.Disabled ? " disabled" : string.Empty;
                var encodedValue = WebUtility.HtmlEncode(option.Value);
                var encodedLabel = WebUtility.HtmlEncode(option.Label);
                var normalizedLabel = WebUtility.HtmlEncode(option.Label.ToLowerInvariant());
                var description = string.IsNullOrWhiteSpace(option.Description)
                    ? string.Empty
                    : $" <small>{WebUtility.HtmlEncode(option.Description)}</small>";
                html.Append($"<label class=\"efui-collection-picker-option\" data-search-text=\"{normalizedLabel}\" style=\"display:block; margin-bottom:0.25rem;\">");
                html.Append($"<input name=\"{fieldName}\" type=\"checkbox\" value=\"{encodedValue}\"{selected}{disabled} /> <span>{encodedLabel}</span>{description}");
                html.Append("</label>");
            }
        }

        html.Append("</div></div>");
    }

    private static void RenderRelatedManagementLinks(StringBuilder html, string routePrefix, EntityMetadata entity)
    {
        html.Append("<section><h2>Related rows</h2>");
        foreach (var link in entity.RelatedManagementLinks)
        {
            html.Append($"<div><label>{WebUtility.HtmlEncode(link.Name)}</label> <a href=\"{routePrefix}/{link.RouteName}\">Manage related rows</a></div>");
        }

        html.Append("</section>");
    }

    private static void RenderCollectionPickerScript(StringBuilder html)
    {
        html.Append("<script>");
        html.Append("document.addEventListener('input',function(event){");
        html.Append("if(!(event.target instanceof HTMLInputElement)||event.target.dataset.role!=='collection-filter'){return;}");
        html.Append("var query=event.target.value.toLowerCase();");
        html.Append("var picker=event.target.closest('.efui-collection-picker');");
        html.Append("if(!picker){return;}");
        html.Append("picker.querySelectorAll('.efui-collection-picker-option').forEach(function(option){");
        html.Append("var checkbox=option.querySelector('input[type=checkbox]');");
        html.Append("if(!(checkbox instanceof HTMLInputElement)){return;}");
        html.Append("var matches=(option.dataset.searchText||'').indexOf(query)!==-1;");
        html.Append("option.style.display=checkbox.checked||matches?'block':'none';");
        html.Append("});");
        html.Append("});");
        html.Append("</script>");
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
