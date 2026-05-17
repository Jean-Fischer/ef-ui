namespace EfUi.Core.Binding;

public sealed record BindResult(bool IsSuccess, object? Value, string? Error)
{
    public static BindResult Success(object? value) => new(true, value, null);

    public static BindResult Failure(string error) => new(false, null, error);
}
