namespace EfUi.Core.Rendering;

public static class EntityDisplayLabelResolver
{
    private static readonly string[] PreferredPropertyNames = ["Name", "Title", "Email"];

    public static string Resolve(object row, string primaryKeyPropertyName)
        => Resolve(row, null, primaryKeyPropertyName);

    public static string Resolve(object row, string? displayPropertyName, string primaryKeyPropertyName)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rowType = row.GetType();
        var explicitLabel = GetPropertyText(rowType, row, displayPropertyName);
        if (!string.IsNullOrWhiteSpace(explicitLabel))
        {
            return explicitLabel;
        }

        foreach (var propertyName in PreferredPropertyNames)
        {
            if (string.Equals(propertyName, displayPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            var value = GetPropertyText(rowType, row, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var primaryKeyValue = rowType.GetProperty(primaryKeyPropertyName)?.GetValue(row);
        return FormatValue(primaryKeyValue);
    }

    private static string? GetPropertyText(Type rowType, object row, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        return rowType.GetProperty(propertyName)?.GetValue(row)?.ToString();
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
}
