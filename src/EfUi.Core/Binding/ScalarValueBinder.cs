using System.Globalization;

namespace EfUi.Core.Binding;

public sealed class ScalarValueBinder : IScalarValueBinder
{
    public BindResult Bind(Type targetType, string? rawValue)
    {
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (actualType == typeof(string))
            {
                return BindResult.Success(rawValue ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(rawValue) && Nullable.GetUnderlyingType(targetType) is not null)
            {
                return BindResult.Success(null);
            }

            return actualType switch
            {
                _ when actualType == typeof(bool) => BindResult.Success(bool.Parse(rawValue!)),
                _ when actualType == typeof(byte) => BindResult.Success(byte.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(short) => BindResult.Success(short.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(int) => BindResult.Success(int.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(long) => BindResult.Success(long.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(float) => BindResult.Success(float.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(double) => BindResult.Success(double.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(decimal) => BindResult.Success(decimal.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(DateTime) => BindResult.Success(DateTime.Parse(rawValue!, CultureInfo.InvariantCulture)),
                _ when actualType == typeof(Guid) => BindResult.Success(Guid.Parse(rawValue!)),
                _ when actualType.IsEnum => BindResult.Success(Enum.Parse(actualType, rawValue!, ignoreCase: true)),
                _ => BindResult.Failure($"Type {GetDisplayName(actualType)} is not supported.")
            };
        }
        catch
        {
            return BindResult.Failure($"Could not parse '{rawValue}' as {GetDisplayName(actualType)}.");
        }
    }

    private static string GetDisplayName(Type type)
    {
        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(byte))
        {
            return "byte";
        }

        if (type == typeof(short))
        {
            return "short";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(long))
        {
            return "long";
        }

        if (type == typeof(float))
        {
            return "float";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(DateTime))
        {
            return "DateTime";
        }

        if (type == typeof(Guid))
        {
            return "Guid";
        }

        return type.Name;
    }
}
