namespace EfUi.Core.Rendering;

public static class EntityDisplayLabelResolver
{
    public static string Resolve(object row, string primaryKeyPropertyName)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rowType = row.GetType();
        foreach (var propertyName in new[] { "Name", "Title", "Email" })
        {
            var value = rowType.GetProperty(propertyName)?.GetValue(row) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var primaryKeyValue = rowType.GetProperty(primaryKeyPropertyName)?.GetValue(row);
        return FormatValue(primaryKeyValue);
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
}
