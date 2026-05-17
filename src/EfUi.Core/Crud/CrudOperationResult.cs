namespace EfUi.Core.Crud;

public sealed record CrudOperationResult(bool IsSuccess, IReadOnlyDictionary<string, string[]> Errors)
{
    public static CrudOperationResult Success() => new(true, new Dictionary<string, string[]>());

    public static CrudOperationResult Failure(string key, string error)
        => new(false, new Dictionary<string, string[]> { [key] = new[] { error } });
}
