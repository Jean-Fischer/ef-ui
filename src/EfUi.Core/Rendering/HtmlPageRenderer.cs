using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public sealed class HtmlPageRenderer : IHtmlPageRenderer
{
    private const string EfUiLabel = "EF UI";
    public string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities, IReadOnlyList<string>? warnings = null, IReadOnlyList<string>? errors = null)
    {
        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-page");
        RenderBreadcrumbs(html, [
            new BreadcrumbItem(EfUiLabel, "/"),
            new BreadcrumbItem(GetMountDisplayName(routePrefix))
        ]);
        html.Append("<section class=\"efui-surface\">");
        html.Append($"<h1>{EfUiLabel}</h1>");
        RenderIssueSummary(html, warnings ?? [], warning: true);
        RenderIssueSummary(html, errors ?? [], warning: false);
        html.Append("<ul class=\"efui-index-list efui-link-grid\">");

        foreach (var entity in entities)
        {
            html.Append($"<li><a href=\"{routePrefix}/{entity.RouteName}\">{WebUtility.HtmlEncode(entity.DisplayName)}</a></li>");
        }

        html.Append("</ul></section></main></body></html>");
        return html.ToString();
    }

    public string RenderList(string routePrefix, EntityMetadata entity, RenderedListView view, bool showActions = true, string? antiForgeryToken = null)
    {
        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-page", BuildTableEnhancementHead(routePrefix));
        RenderBreadcrumbs(html, [
            new BreadcrumbItem(EfUiLabel, "/"),
            new BreadcrumbItem(GetMountDisplayName(routePrefix), routePrefix),
            new BreadcrumbItem(entity.DisplayName)
        ]);
        html.Append("<section class=\"efui-surface\">");
        html.Append($"<h1>{WebUtility.HtmlEncode(entity.DisplayName)}</h1>");
        if (showActions)
        {
            html.Append("<div class=\"efui-page-actions\">");
            html.Append($"<a class=\"efui-primary-link\" href=\"{routePrefix}/{entity.RouteName}/new\">Create New</a>");
            AppendClosingDivTag(html);
        }
        RenderTableStatus(html, view);
        RenderTableEnhancementShell(html, routePrefix, entity, view, showActions, antiForgeryToken);
        html.Append("<div class=\"efui-table-wrapper\" data-role=\"efui-table-fallback\">");
        html.Append("<table class=\"efui-table\"><thead><tr>");

        foreach (var property in entity.AllProperties)
        {
            html.Append($"<th>{WebUtility.HtmlEncode(property.Name)}</th>");
        }

        if (showActions)
        {
            html.Append("<th>Actions</th>");
        }

        html.Append("</tr></thead><tbody>");

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

            if (showActions)
            {
                html.Append("<td class=\"efui-row-actions\">");
                html.Append(BuildRowActionsMarkup(routePrefix, entity, row.Key, antiForgeryToken));
                html.Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</tbody></table></div></section></main></body></html>");
        return html.ToString();
    }

    public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null, string? antiForgeryToken = null)
        => RenderEditForm(routePrefix, entity, model, isCreate, errors, null, submittedValues, fieldOptions, antiForgeryToken);

    private static void RenderTableStatus(StringBuilder html, RenderedListView view)
    {
        var hasWarnings = view.Warnings.Count > 0;
        var hasErrors = view.Errors.Count > 0;
        if (!hasWarnings && !hasErrors)
        {
            return;
        }

        html.Append($"<section class=\"efui-table-status\" data-role=\"efui-table-status\" data-offset=\"{view.Offset}\" data-limit=\"{view.Limit}\">");

        RenderIssueSummary(html, view.Warnings, warning: true);
        RenderIssueSummary(html, view.Errors, warning: false);

        html.Append("</section>");
    }

    private static void RenderIssueSummary(StringBuilder html, IReadOnlyList<string> messages, bool warning)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var summaryClass = warning ? "efui-warning-summary" : "efui-error-summary";
        var itemClass = warning ? "efui-warning" : "efui-error";
        html.Append($"<div class=\"{summaryClass}\">");
        foreach (var message in messages)
        {
            html.Append($"<div class=\"{itemClass}\">{WebUtility.HtmlEncode(message)}</div>");
        }

        AppendClosingDivTag(html);
    }

    private static void AppendClosingDivTag(StringBuilder html)
    {
        html.Append('<');
        html.Append("/div>");
    }

    private static void RenderBreadcrumbs(StringBuilder html, IReadOnlyList<BreadcrumbItem> items)
    {
        html.Append("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\"><ol class=\"efui-breadcrumb-list\">");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var isCurrent = index == items.Count - 1 || string.IsNullOrWhiteSpace(item.Href);
            html.Append("<li class=\"efui-breadcrumb-item\">");
            if (isCurrent)
            {
                html.Append($"<span class=\"efui-breadcrumb-current\">{WebUtility.HtmlEncode(item.Label)}</span>");
            }
            else
            {
                html.Append($"<a class=\"efui-breadcrumb-link\" href=\"{item.Href}\">{WebUtility.HtmlEncode(item.Label)}</a>");
            }

            html.Append("</li>");
        }

        html.Append("</ol></nav>");
    }

    private static string GetMountDisplayName(string routePrefix)
    {
        var segment = routePrefix.Trim('/');
        if (string.IsNullOrWhiteSpace(segment))
        {
            return EfUiLabel;
        }

        var words = segment
            .Split(['/', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

        return string.Join(' ', words);
    }

    private sealed record BreadcrumbItem(string Label, string? Href = null);

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
        => $"<link rel=\"stylesheet\" href=\"{routePrefix}/assets/tabulator.min.css\" /><link rel=\"stylesheet\" href=\"{routePrefix}/assets/efui-table.css\" /><script src=\"{routePrefix}/assets/tabulator.min.js\"></script><script defer src=\"{routePrefix}/assets/efui-table.js\"></script>";

    private static void RenderTableEnhancementShell(StringBuilder html, string routePrefix, EntityMetadata entity, RenderedListView view, bool showActions, string? antiForgeryToken)
    {
        html.Append("<section class=\"efui-table-enhancement\" data-role=\"efui-table-enhancement\">");
        html.Append("<div class=\"efui-table-host\" data-role=\"efui-table-host\"></div>");
        html.Append("<script type=\"application/json\" data-role=\"efui-table-config\">");
        html.Append(RenderedListPayloadFactory.Serialize(routePrefix, entity, view, showActions, antiForgeryToken));
        html.Append("</script></section>");
    }

    string IHtmlPageRenderer.RenderErrorPage(string routePrefix, string title, IReadOnlyList<string> messages)
        => RenderErrorPage(routePrefix, title, messages);

    public static string RenderErrorPage(string routePrefix, string title, IReadOnlyList<string> messages)
    {
        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-page");
        RenderBreadcrumbs(html, [
            new BreadcrumbItem(EfUiLabel, "/"),
            new BreadcrumbItem(GetMountDisplayName(routePrefix), routePrefix),
            new BreadcrumbItem(title)
        ]);
        html.Append("<section class=\"efui-surface\">");
        html.Append($"<h1>{WebUtility.HtmlEncode(title)}</h1>");
        RenderIssueSummary(html, messages, warning: false);
        html.Append($"<a class=\"efui-primary-link\" href=\"{routePrefix}\">Back</a>");
        html.Append("</section></main></body></html>");
        return html.ToString();
    }

    private static string BuildRowActionsMarkup(string routePrefix, EntityMetadata entity, string rowKey, string? antiForgeryToken)
    {
        var escapedKey = EscapeRouteSegment(rowKey);
        var antiforgeryField = AntiforgeryMarkup.BuildHiddenInput(antiForgeryToken);
        return $"<a class=\"efui-row-action-link\" href=\"{routePrefix}/{entity.RouteName}/{escapedKey}/edit\">Edit</a><form class=\"efui-row-action-form\" method=\"post\" action=\"{routePrefix}/{entity.RouteName}/{escapedKey}/delete\">{antiforgeryField}<button class=\"efui-row-action-button\" type=\"submit\">Delete</button></form>";
    }

    public string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null, string? antiForgeryToken = null)
    {
        var html = new StringBuilder();
        AppendDocumentStart(html, routePrefix, "efui-form-page");
        RenderBreadcrumbs(html, [
            new BreadcrumbItem(EfUiLabel, "/"),
            new BreadcrumbItem(GetMountDisplayName(routePrefix), routePrefix),
            new BreadcrumbItem(entity.DisplayName, $"{routePrefix}/{entity.RouteName}"),
            new BreadcrumbItem(isCreate ? "New" : "Edit")
        ]);
        html.Append($"<form class=\"efui-form\" method=\"post\" action=\"{GetEditFormAction(routePrefix, entity, isCreate, key)}\">{AntiforgeryMarkup.BuildHiddenInput(antiForgeryToken)}");
        html.Append($"<h1 class=\"efui-form-title\">{WebUtility.HtmlEncode(entity.DisplayName)}</h1>");

        RenderFormErrors(html, errors);

        if (!isCreate)
        {
            RenderPrimaryKeyField(html, entity, model, key);
        }

        var editableFields = isCreate ? entity.CreateEditableFields : entity.UpdateEditableFields;

        foreach (var field in editableFields)
        {
            RenderEditableField(html, field, model, submittedValues, fieldOptions);
        }

        if (HasCollectionFields(editableFields))
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

    private static string GetEditFormAction(string routePrefix, EntityMetadata entity, bool isCreate, object? key)
        => isCreate
            ? $"{routePrefix}/{entity.RouteName}"
            : $"{routePrefix}/{entity.RouteName}/{EscapeRouteSegment(key)}";

    private static void RenderFormErrors(StringBuilder html, IReadOnlyDictionary<string, string[]> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

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

    private static void RenderPrimaryKeyField(StringBuilder html, EntityMetadata entity, object? model, object? key)
    {
        var keyValue = GetFieldValue(model, entity.PrimaryKeyProperty.Name, null, null, key);

        html.Append("<div class=\"efui-field\">");
        html.Append($"<label class=\"efui-label\">{WebUtility.HtmlEncode(entity.PrimaryKeyProperty.Name)}</label>");
        html.Append($"<span class=\"efui-readonly-value\">{WebUtility.HtmlEncode(keyValue)}</span>");
        html.Append("</div>");
    }

    private static void RenderEditableField(StringBuilder html, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions)
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

    private static bool HasCollectionFields(IReadOnlyList<EditableFieldMetadata> editableFields)
        => editableFields.Any(field => field.Kind == EditableFieldKind.Collection);

    private static string GetFieldValue(object? model, string propertyName, IReadOnlyDictionary<string, string[]>? submittedValues, string? submittedFieldName, object? fallbackValue = null)
    {
        if (submittedValues is not null && submittedFieldName is not null && submittedValues.TryGetValue(submittedFieldName, out var submittedValue))
        {
            return submittedValue.FirstOrDefault() ?? string.Empty;
        }

        var source = model is null ? fallbackValue : model.GetType().GetProperty(propertyName)?.GetValue(model);
        return FormatValue(source);
    }

    private const string DateTimeInputPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})?$";
    private const string DateTimeInputPlaceholder = "2026-05-17T10:30:00Z";

    private static void RenderScalarField(StringBuilder html, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues)
    {
        var actualType = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
        var value = GetScalarFieldValue(field, model, submittedValues);

        if (actualType == typeof(bool))
        {
            if (Nullable.GetUnderlyingType(field.ValueType) is not null)
            {
                RenderNullableBooleanField(html, field, value);
            }
            else
            {
                RenderBooleanField(html, field, value);
            }

            return;
        }

        if (actualType == typeof(DateTime))
        {
            RenderDateTimeField(html, field, value);
            return;
        }

        if (actualType.IsEnum)
        {
            RenderEnumField(html, field, value, actualType);
            return;
        }

        if (IsNumberType(actualType))
        {
            RenderNumberField(html, field, value, actualType);
            return;
        }

        RenderTextField(html, field, value);
    }

    private static void RenderTextField(StringBuilder html, EditableFieldMetadata field, string value)
        => html.Append($"<input class=\"efui-input\" name=\"{field.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" />");

    private static void RenderNumberField(StringBuilder html, EditableFieldMetadata field, string value, Type actualType)
    {
        var step = IsIntegralType(actualType) ? "1" : "any";
        html.Append($"<input class=\"efui-input\" type=\"number\" step=\"{step}\" name=\"{field.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" />");
    }

    private static void RenderDateTimeField(StringBuilder html, EditableFieldMetadata field, string value)
        => html.Append($"<input class=\"efui-input\" name=\"{field.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" placeholder=\"{DateTimeInputPlaceholder}\" pattern=\"{DateTimeInputPattern}\" />");

    private static void RenderBooleanField(StringBuilder html, EditableFieldMetadata field, string value)
    {
        var isChecked = bool.TryParse(value, out var parsed) && parsed;
        html.Append($"<input type=\"checkbox\" name=\"{field.Name}\" value=\"true\"{(isChecked ? " checked" : string.Empty)} />");
        html.Append($"<input type=\"hidden\" name=\"{field.Name}\" value=\"false\" />");
    }

    private static void RenderNullableBooleanField(StringBuilder html, EditableFieldMetadata field, string value)
    {
        var selectedValue = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : bool.TryParse(value, out var parsed)
                ? parsed.ToString().ToLowerInvariant()
                : string.Empty;

        html.Append($"<select class=\"efui-select\" name=\"{field.Name}\">");
        html.Append($"<option value=\"\"{(selectedValue.Length == 0 ? " selected" : string.Empty)}></option>");
        html.Append($"<option value=\"true\"{(string.Equals(selectedValue, "true", StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty)}>True</option>");
        html.Append($"<option value=\"false\"{(string.Equals(selectedValue, "false", StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty)}>False</option>");
        html.Append("</select>");
    }

    private static void RenderEnumField(StringBuilder html, EditableFieldMetadata field, string value, Type actualType)
    {
        var allowBlank = Nullable.GetUnderlyingType(field.ValueType) is not null || !field.IsRequired;
        html.Append($"<select class=\"efui-select\" name=\"{field.Name}\">");

        if (allowBlank)
        {
            html.Append($"<option value=\"\"{(string.IsNullOrWhiteSpace(value) ? " selected" : string.Empty)}></option>");
        }

        foreach (var enumValue in Enum.GetValues(actualType).Cast<object>())
        {
            var optionValue = Enum.GetName(actualType, enumValue) ?? enumValue.ToString() ?? string.Empty;
            var selected = string.Equals(value, optionValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, enumValue.ToString(), StringComparison.OrdinalIgnoreCase)
                ? " selected"
                : string.Empty;
            html.Append($"<option value=\"{WebUtility.HtmlEncode(optionValue)}\"{selected}>{WebUtility.HtmlEncode(optionValue)}</option>");
        }

        html.Append("</select>");
    }

    private static string GetScalarFieldValue(EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues)
    {
        if (submittedValues is not null && submittedValues.TryGetValue(field.Name, out var submittedValue))
        {
            return submittedValue.FirstOrDefault() ?? string.Empty;
        }

        var propertyName = field.ScalarPropertyName ?? field.Name;
        var source = model is null ? null : model.GetType().GetProperty(propertyName)?.GetValue(model);
        return FormatScalarValue(source, field.ValueType);
    }

    private static bool IsNumberType(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;

        return actual == typeof(byte)
            || actual == typeof(short)
            || actual == typeof(int)
            || actual == typeof(long)
            || actual == typeof(float)
            || actual == typeof(double)
            || actual == typeof(decimal);
    }

    private static bool IsIntegralType(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;

        return actual == typeof(byte)
            || actual == typeof(short)
            || actual == typeof(int)
            || actual == typeof(long);
    }

    private static string FormatScalarValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return actualType switch
        {
            _ when actualType == typeof(bool) => bool.Parse(value.ToString()!).ToString().ToLowerInvariant(),
            _ when actualType == typeof(DateTime) => ((DateTime)value).ToString("O", CultureInfo.InvariantCulture),
            _ when actualType.IsEnum => Enum.GetName(actualType, value) ?? value.ToString() ?? string.Empty,
            _ when IsNumberType(actualType) => value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
                : value.ToString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static void RenderReferenceField(StringBuilder html, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions)
    {
        var currentValue = GetFieldValue(model, field.ScalarPropertyName!, submittedValues, field.Name);

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
        html.Append("function renderResults(){resultsHost.innerHTML='';var query=searchInput.value.toLowerCase().trim();var available=options.filter(function(option){return !option.selected&&(!query||option.searchText.indexOf(query)!==-1);});if(available.length===0){var empty=document.createElement('div');empty.className='efui-chip-picker-empty';empty.textContent='No matching options';resultsHost.appendChild(empty);return;}available.forEach(function(option){var button=document.createElement('button');button.type='button';button.className='efui-chip-picker-result'+(option.disabled?' efui-chip-picker-result-disabled':'');button.dataset.role='chip-option';button.dataset.value=option.value;button.disabled=option.disabled;if(option.disabled){button.setAttribute('aria-disabled','true');}button.textContent=option.label;if(option.description){var description=document.createElement('small');description.className='efui-chip-picker-description';description.textContent=option.description;button.appendChild(document.createElement('br'));button.appendChild(description);}resultsHost.appendChild(button);});}");
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
