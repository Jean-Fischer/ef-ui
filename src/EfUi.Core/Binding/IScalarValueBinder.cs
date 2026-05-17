namespace EfUi.Core.Binding;

public interface IScalarValueBinder
{
    BindResult Bind(Type targetType, string? rawValue);
}
