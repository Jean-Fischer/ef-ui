using System.Net;
using System.Text;
using System.Text.Json;
using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public sealed class HtmlPageRenderer : IHtmlPageRenderer
{
    public string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities)
    {
        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-page");
        html.Append("<section class=\"efui-surface\">");
        html.Append("<h1>EF UI</h1>");
        html.Append("<ul class=\"efui-index-list efui-link-grid\">");

        foreach (var entity in entities)
        {
            html.Append($"<li><a href=\"{routePrefix}/{entity.RouteName}\">{WebUtility.HtmlEncode(entity.DisplayName)}</a></li>");
        }

        html.Append("</ul></section></main></body></html>");
        return html.ToString();
    }

    public string RenderList(string routePrefix, EntityMetadata entity, RenderedListView view)
    {
        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-page", BuildTableEnhancementHead(routePrefix));
        html.Append("<section class=\"efui-surface\">");
        html.Append($"<h1>{WebUtility.HtmlEncode(entity.DisplayName)}</h1>");
        html.Append("<div class=\"efui-page-actions\">");
        html.Append($"<a class=\"efui-primary-link\" href=\"{routePrefix}/{entity.RouteName}/new\">Create New</a>");
        html.Append("</div>");
        RenderQueryBuilder(html, routePrefix, entity, view);
        RenderTableEnhancementShell(html, routePrefix, entity, view);
        html.Append("<div class=\"efui-table-wrapper\" data-role=\"efui-table-fallback\">");
        html.Append("<table class=\"efui-table\"><thead><tr>");

        foreach (var property in entity.AllProperties)
        {
            html.Append($"<th>{WebUtility.HtmlEncode(property.Name)}</th>");
        }

        html.Append("<th>Actions</th></tr></thead><tbody>");

        foreach (var row in view.Rows)
        {
            html.Append("<tr>");

            foreach (var property in entity.AllProperties)
            {
                row.Cells.TryGetValue(property.Name, out var value);
                html.Append("<td>");
                RenderListCell(html, value);
                html.Append("</td>");
            }

            html.Append("<td class=\"efui-row-actions\">");
            html.Append(BuildRowActionsMarkup(routePrefix, entity, row.Key));
            html.Append("</td></tr>");
        }

        html.Append("</tbody></table></div></section></main></body></html>");
        return html.ToString();
    }

    public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null)
        => RenderEditForm(routePrefix, entity, model, isCreate, errors, null, submittedValues, fieldOptions);

    private static void RenderQueryBuilder(StringBuilder html, string routePrefix, EntityMetadata entity, RenderedListView view)
    {
        var action = $"{routePrefix}/{entity.RouteName}";
        var activeFilter = view.Filters.FirstOrDefault();
        var activeSort = view.Sorts.FirstOrDefault();

        html.Append($"<section class=\"efui-query-builder\" data-offset=\"{view.Offset}\" data-limit=\"{view.Limit}\">");

        if (view.Errors.Count > 0)
        {
            html.Append("<div class=\"efui-error-summary\">");
            foreach (var error in view.Errors)
            {
                html.Append($"<div class=\"efui-error\">{WebUtility.HtmlEncode(error)}</div>");
            }

            html.Append("</div>");
        }

        html.Append($"<form class=\"efui-query-builder-form\" method=\"get\" action=\"{action}\" data-role=\"efui-query-form\">");
        html.Append("<div class=\"efui-query-builder-controls\">");
        html.Append("<label class=\"efui-query-builder-field\"><span class=\"efui-label\">Filter field</span>");
        RenderQueryFieldSelect(html, "filter.0.field", entity, activeFilter?.Field, includeEmptyOption: true);
        html.Append("</label>");
        html.Append("<label class=\"efui-query-builder-field\"><span class=\"efui-label\">Operator</span>");
        RenderQueryOperatorSelect(html, activeFilter?.Operator);
        html.Append("</label>");
        html.Append($"<label class=\"efui-query-builder-field efui-query-builder-value\"><span class=\"efui-label\">Value</span><input class=\"efui-input\" name=\"filter.0.value\" value=\"{WebUtility.HtmlEncode(activeFilter?.Value ?? string.Empty)}\" /></label>");
        html.Append("<label class=\"efui-query-builder-field\"><span class=\"efui-label\">Sort field</span>");
        RenderQueryFieldSelect(html, "sort.0.field", entity, activeSort?.Field, includeEmptyOption: true);
        html.Append("</label>");
        html.Append("<label class=\"efui-query-builder-field\"><span class=\"efui-label\">Direction</span>");
        RenderSortDirectionSelect(html, activeSort?.Direction);
        html.Append("</label>");
        html.Append($"<input type=\"hidden\" name=\"offset\" value=\"0\" />");
        html.Append($"<input type=\"hidden\" name=\"limit\" value=\"{view.Limit}\" />");
        html.Append("</div>");
        html.Append("<div class=\"efui-query-builder-actions\">");
        html.Append("<button class=\"efui-button\" type=\"submit\">Apply</button>");
        html.Append($"<a class=\"efui-query-builder-clear\" href=\"{action}\">Clear</a>");
        html.Append("</div></form>");

        html.Append("<div class=\"efui-query-builder-group\"><h2>Filters</h2>");
        if (view.Filters.Count == 0)
        {
            html.Append("<div class=\"efui-query-builder-empty\">No filters</div>");
        }
        else
        {
            foreach (var filter in view.Filters)
            {
                html.Append($"<div class=\"efui-query-builder-filter\">{WebUtility.HtmlEncode(filter.Field)} {WebUtility.HtmlEncode(filter.Operator)} {WebUtility.HtmlEncode(filter.Value ?? string.Empty)}</div>");
            }
        }

        html.Append("</div>");
        html.Append("<div class=\"efui-query-builder-group\"><h2>Sorts</h2>");
        if (view.Sorts.Count == 0)
        {
            html.Append("<div class=\"efui-query-builder-empty\">No sorts</div>");
        }
        else
        {
            foreach (var sort in view.Sorts)
            {
                html.Append($"<div class=\"efui-query-builder-sort\">{WebUtility.HtmlEncode(sort.Field)} {WebUtility.HtmlEncode(sort.Direction)}</div>");
            }
        }

        html.Append("</div></section>");
    }

    private static void RenderQueryFieldSelect(StringBuilder html, string name, EntityMetadata entity, string? selectedValue, bool includeEmptyOption)
    {
        html.Append($"<select class=\"efui-select\" name=\"{name}\">");
        if (includeEmptyOption)
        {
            html.Append("<option value=\"\"></option>");
        }

        foreach (var property in entity.AllProperties)
        {
            var selected = string.Equals(property.Name, selectedValue, StringComparison.Ordinal) ? " selected" : string.Empty;
            html.Append($"<option value=\"{WebUtility.HtmlEncode(property.Name)}\"{selected}>{WebUtility.HtmlEncode(property.Name)}</option>");
        }

        html.Append("</select>");
    }

    private static void RenderQueryOperatorSelect(StringBuilder html, string? selectedValue)
    {
        html.Append("<select class=\"efui-select\" name=\"filter.0.op\">");
        html.Append("<option value=\"\"></option>");
        foreach (var op in new[] { "contains", "eq" })
        {
            var selected = string.Equals(op, selectedValue, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty;
            html.Append($"<option value=\"{op}\"{selected}>{op}</option>");
        }

        html.Append("</select>");
    }

    private static void RenderSortDirectionSelect(StringBuilder html, string? selectedValue)
    {
        html.Append("<select class=\"efui-select\" name=\"sort.0.dir\">");
        html.Append("<option value=\"\"></option>");
        foreach (var direction in new[] { "asc", "desc" })
        {
            var selected = string.Equals(direction, selectedValue, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty;
            html.Append($"<option value=\"{direction}\"{selected}>{direction}</option>");
        }

        html.Append("</select>");
    }

    private static void RenderListCell(StringBuilder html, RenderedListCell? value)
    {
        var text = WebUtility.HtmlEncode(value?.Text ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(value?.Href))
        {
            html.Append($"<a class=\"efui-cell-link\" href=\"{value.Href}\">{text}</a>");
            return;
        }

        html.Append(text);
    }

    private static string BuildTableEnhancementHead(string routePrefix)
        => $"<link rel=\"stylesheet\" href=\"https://unpkg.com/tabulator-tables@6.3.0/dist/css/tabulator.min.css\" /><link rel=\"stylesheet\" href=\"{routePrefix}/assets/efui-table.css\" /><script src=\"https://unpkg.com/tabulator-tables@6.3.0/dist/js/tabulator.min.js\"></script><script defer src=\"{routePrefix}/assets/efui-table.js\"></script>";

    private static void RenderTableEnhancementShell(StringBuilder html, string routePrefix, EntityMetadata entity, RenderedListView view)
    {
        html.Append("<section class=\"efui-table-enhancement\" data-role=\"efui-table-enhancement\">");
        html.Append("<div class=\"efui-table-loading\" data-role=\"efui-table-loading\" hidden aria-live=\"polite\">Loading table…</div>");
        html.Append("<div class=\"efui-table-host\" data-role=\"efui-table-host\"></div>");
        html.Append("<script type=\"application/json\" data-role=\"efui-table-config\">");
        html.Append(BuildTableEnhancementConfig(routePrefix, entity, view));
        html.Append("</script></section>");
    }

    private static string BuildTableEnhancementConfig(string routePrefix, EntityMetadata entity, RenderedListView view)
    {
        var payload = new
        {
            library = "tabulator",
            entity = entity.RouteName,
            columns = entity.AllProperties
                .Select(property => new { field = property.Name, title = property.Name, headerSort = true })
                .Concat(new[] { new { field = "__actions", title = "Actions", headerSort = false } })
                .ToList(),
            rows = view.Rows.Select(row =>
            {
                var values = row.Cells.ToDictionary(
                    cell => cell.Key,
                    cell => (object?)new { text = cell.Value.Text, href = cell.Value.Href },
                    StringComparer.Ordinal);
                values["__actions"] = BuildRowActionsMarkup(routePrefix, entity, row.Key);
                return values;
            }).ToList(),
            query = new
            {
                filters = view.Filters,
                sorts = view.Sorts,
                offset = view.Offset,
                limit = view.Limit
            }
        };

        return JsonSerializer.Serialize(payload).Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRowActionsMarkup(string routePrefix, EntityMetadata entity, string rowKey)
    {
        var escapedKey = EscapeRouteSegment(rowKey);
        return $"<a class=\"efui-row-action-link\" href=\"{routePrefix}/{entity.RouteName}/{escapedKey}/edit\">Edit</a><form class=\"efui-row-action-form\" method=\"post\" action=\"{routePrefix}/{entity.RouteName}/{escapedKey}/delete\"><button class=\"efui-row-action-button\" type=\"submit\">Delete</button></form>";
    }

    public string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null)
    {
        var action = isCreate
            ? $"{routePrefix}/{entity.RouteName}"
            : $"{routePrefix}/{entity.RouteName}/{EscapeRouteSegment(key)}";

        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-form-page");
        html.Append($"<form class=\"efui-form\" method=\"post\" action=\"{action}\">");
        html.Append($"<h1 class=\"efui-form-title\">{WebUtility.HtmlEncode(entity.DisplayName)}</h1>");

        if (errors.Count > 0)
        {
            html.Append("<div class=\"efui-error-summary\">");
            foreach (var error in errors)
            {
                foreach (var message in error.Value)
                {
                    html.Append($"<div class=\"efui-error\">{WebUtility.HtmlEncode(message)}</div>");
                }
            }

            html.Append("</div>");
        }

        if (!isCreate)
        {
            var keyValue = model is null
                ? FormatValue(key)
                : FormatValue(model.GetType().GetProperty(entity.PrimaryKeyProperty.Name)?.GetValue(model));

            html.Append("<div class=\"efui-field\">");
            html.Append($"<label class=\"efui-label\">{WebUtility.HtmlEncode(entity.PrimaryKeyProperty.Name)}</label>");
            html.Append($"<span class=\"efui-readonly-value\">{WebUtility.HtmlEncode(keyValue)}</span>");
            html.Append("</div>");
        }

        var editableFields = isCreate ? entity.CreateEditableFields : entity.UpdateEditableFields;

        foreach (var field in editableFields)
        {
            html.Append("<div class=\"efui-field\">");
            html.Append($"<label class=\"efui-label\">{WebUtility.HtmlEncode(field.Name)}</label>");

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

            html.Append("</div>");
        }

        if (editableFields.Any(field => field.Kind == EditableFieldKind.Collection))
        {
            RenderCollectionPickerScript(html);
        }

        if (!isCreate && entity.RelatedManagementLinks.Any())
        {
            RenderRelatedManagementLinks(html, routePrefix, entity, key);
        }

        html.Append("<button class=\"efui-button\" type=\"submit\">Save</button></form>");
        html.Append("</main></body></html>");
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

        html.Append($"<input class=\"efui-input\" name=\"{field.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" />");
    }

    private static void RenderReferenceField(StringBuilder html, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions)
    {
        var currentValue = submittedValues is not null && submittedValues.TryGetValue(field.Name, out var submittedValue)
            ? submittedValue.FirstOrDefault() ?? string.Empty
            : model is null
                ? string.Empty
                : FormatValue(model.GetType().GetProperty(field.ScalarPropertyName!)?.GetValue(model));

        html.Append($"<select class=\"efui-select\" name=\"{field.Name}\">");
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
        html.Append($"<div class=\"efui-chip-picker\" data-role=\"chip-picker\" data-field-name=\"{fieldName}\">");
        html.Append("<div class=\"efui-chip-picker-selected\" data-role=\"chip-picker-selected\"></div>");
        html.Append($"<input type=\"search\" class=\"efui-input efui-search-input\" data-role=\"chip-picker-search\" placeholder=\"Search {fieldName}...\" />");
        html.Append("<div class=\"efui-chip-picker-results\" data-role=\"chip-picker-results\"></div>");
        html.Append("<div class=\"efui-chip-picker-hidden-inputs\" data-role=\"chip-picker-hidden-inputs\"></div>");
        html.Append("<div class=\"efui-chip-picker-fallback\">");

        if (fieldOptions is not null && fieldOptions.TryGetValue(field.Name, out var options))
        {
            foreach (var option in options)
            {
                var selected = option.Selected ? " checked" : string.Empty;
                var disabled = option.Disabled ? " disabled" : string.Empty;
                var encodedValue = WebUtility.HtmlEncode(option.Value);
                var encodedLabel = WebUtility.HtmlEncode(option.Label);
                var encodedDescription = WebUtility.HtmlEncode(option.Description ?? string.Empty);
                var normalizedLabel = WebUtility.HtmlEncode(option.Label.ToLowerInvariant());
                var description = string.IsNullOrWhiteSpace(option.Description)
                    ? string.Empty
                    : $" <small class=\"efui-chip-picker-description\">{WebUtility.HtmlEncode(option.Description)}</small>";
                html.Append($"<label class=\"efui-chip-picker-option\" data-search-text=\"{normalizedLabel}\">");
                html.Append($"<input name=\"{fieldName}\" type=\"checkbox\" value=\"{encodedValue}\"{selected}{disabled} data-label=\"{encodedLabel}\" data-description=\"{encodedDescription}\" /> <span>{encodedLabel}</span>{description}");
                html.Append("</label>");
            }
        }

        html.Append("</div></div>");
    }

    private static void RenderRelatedManagementLinks(StringBuilder html, string routePrefix, EntityMetadata entity, object? key)
    {
        html.Append("<section class=\"efui-related-links\"><h2 class=\"efui-related-links-title\">Related rows</h2>");
        foreach (var link in entity.RelatedManagementLinks)
        {
            var href = $"{routePrefix}/{link.RouteName}?filter.0.field={Uri.EscapeDataString(link.FilterFieldName)}&filter.0.op=eq&filter.0.value={Uri.EscapeDataString(FormatValue(key))}";
            html.Append($"<div class=\"efui-related-link\"><label class=\"efui-label\">{WebUtility.HtmlEncode(link.Name)}</label> <a class=\"efui-related-link-action\" href=\"{href}\">Manage related rows</a></div>");
        }

        html.Append("</section>");
    }

    private static void RenderCollectionPickerScript(StringBuilder html)
    {
        html.Append("<script>");
        html.Append("document.addEventListener('DOMContentLoaded',function(){");
        html.Append("document.querySelectorAll('[data-role=\"chip-picker\"]').forEach(function(picker){");
        html.Append("if(!(picker instanceof HTMLElement)){return;}");
        html.Append("var selectedHost=picker.querySelector('[data-role=\"chip-picker-selected\"]');");
        html.Append("var searchInput=picker.querySelector('[data-role=\"chip-picker-search\"]');");
        html.Append("var resultsHost=picker.querySelector('[data-role=\"chip-picker-results\"]');");
        html.Append("var hiddenHost=picker.querySelector('[data-role=\"chip-picker-hidden-inputs\"]');");
        html.Append("var fallbackHost=picker.querySelector('.efui-chip-picker-fallback');");
        html.Append("if(!(selectedHost instanceof HTMLElement)||!(searchInput instanceof HTMLInputElement)||!(resultsHost instanceof HTMLElement)||!(hiddenHost instanceof HTMLElement)||!(fallbackHost instanceof HTMLElement)){return;}");
        html.Append("var fieldName=picker.dataset.fieldName||'';");
        html.Append("var options=Array.from(fallbackHost.querySelectorAll('input[type=checkbox]')).filter(function(input){return input instanceof HTMLInputElement;}).map(function(input){return {value:input.value,label:input.dataset.label||input.value,description:input.dataset.description||'',searchText:((input.dataset.label||input.value)+' '+(input.dataset.description||'')).toLowerCase(),selected:input.checked,disabled:input.disabled};});");
        html.Append("function syncHiddenInputs(){hiddenHost.innerHTML='';options.filter(function(option){return option.selected;}).forEach(function(option){var input=document.createElement('input');input.type='hidden';input.name=fieldName;input.value=option.value;hiddenHost.appendChild(input);});}");
        html.Append("function renderChips(){selectedHost.innerHTML='';selectedHost.className='efui-chip-list';var selected=options.filter(function(option){return option.selected;});if(selected.length===0){var empty=document.createElement('div');empty.className='efui-chip-picker-empty';empty.textContent='No items selected';selectedHost.appendChild(empty);return;}selected.forEach(function(option){var chip=document.createElement('span');chip.className='efui-chip';var label=document.createElement('span');label.textContent=option.label;chip.appendChild(label);if(!option.disabled){var remove=document.createElement('button');remove.type='button';remove.className='efui-chip-remove';remove.dataset.role='chip-remove';remove.dataset.value=option.value;remove.setAttribute('aria-label','Remove '+option.label);remove.textContent='×';chip.appendChild(remove);}selectedHost.appendChild(chip);});}");
        html.Append("function renderResults(){resultsHost.innerHTML='';var query=searchInput.value.toLowerCase().trim();var available=options.filter(function(option){return !option.selected&&!option.disabled&&(!query||option.searchText.indexOf(query)!==-1);});if(available.length===0){var empty=document.createElement('div');empty.className='efui-chip-picker-empty';empty.textContent='No matching options';resultsHost.appendChild(empty);return;}available.forEach(function(option){var button=document.createElement('button');button.type='button';button.className='efui-chip-picker-result';button.dataset.role='chip-option';button.dataset.value=option.value;button.textContent=option.label;if(option.description){var description=document.createElement('small');description.className='efui-chip-picker-description';description.textContent=option.description;button.appendChild(document.createElement('br'));button.appendChild(description);}resultsHost.appendChild(button);});}");
        html.Append("function rerender(){syncHiddenInputs();renderChips();renderResults();}");
        html.Append("picker.addEventListener('click',function(event){var target=event.target;if(!(target instanceof HTMLElement)){return;}var remove=target.closest('[data-role=\"chip-remove\"]');if(remove instanceof HTMLElement){var value=remove.dataset.value||'';options.forEach(function(option){if(option.value===value&&!option.disabled){option.selected=false;}});rerender();return;}var add=target.closest('[data-role=\"chip-option\"]');if(add instanceof HTMLElement){var value=add.dataset.value||'';options.forEach(function(option){if(option.value===value&&!option.disabled){option.selected=true;}});searchInput.focus();rerender();}});");
        html.Append("searchInput.addEventListener('input',renderResults);");
        html.Append("Array.from(fallbackHost.querySelectorAll('input[type=checkbox]')).forEach(function(input){if(input instanceof HTMLInputElement){input.disabled=true;}});");
        html.Append("picker.classList.add('efui-chip-picker-enhanced');");
        html.Append("rerender();");
        html.Append("});");
        html.Append("});");
        html.Append("</script>");
    }

    private static void AppendDocumentStart(StringBuilder html, string routePrefix, string mainClass, string? extraHead = null)
    {
        html.Append("<html><head>");
        html.Append("<meta charset=\"utf-8\" />");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        html.Append($"<link rel=\"stylesheet\" href=\"{routePrefix}/assets/efui.css\" />");
        if (!string.IsNullOrWhiteSpace(extraHead))
        {
            html.Append(extraHead);
        }

        html.Append($"</head><body class=\"efui-body\"><main class=\"{mainClass}\">");
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
