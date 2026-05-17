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

            if (actualType == typeof(int))
            {
                return BindResult.Success(int.Parse(rawValue!, CultureInfo.InvariantCulture));
            }

            if (actualType == typeof(bool))
            {
                return BindResult.Success(bool.Parse(rawValue!));
            }

            if (actualType == typeof(DateTime))
            {
                return BindResult.Success(DateTime.Parse(rawValue!, CultureInfo.InvariantCulture));
            }

            if (actualType.IsEnum)
            {
                return BindResult.Success(Enum.Parse(actualType, rawValue!, ignoreCase: true));
            }
        }
        catch
        {
            return BindResult.Failure($"Could not parse '{rawValue}' as {GetDisplayName(actualType)}.");
        }

        return BindResult.Failure($"Type {GetDisplayName(actualType)} is not supported.");
    }

    private static string GetDisplayName(Type type)
    {
        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(DateTime))
        {
            return "DateTime";
        }

        return type.Name;
    }
}
