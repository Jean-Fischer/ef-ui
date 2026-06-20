using System.Net;

namespace EfUi.Core.Rendering;

internal static class AntiforgeryMarkup
{
    internal const string TokenFieldName = "__RequestVerificationToken";

    internal static string BuildHiddenInput(string? antiForgeryToken)
        => string.IsNullOrWhiteSpace(antiForgeryToken)
            ? string.Empty
            : $"<input type=\"hidden\" name=\"{TokenFieldName}\" value=\"{WebUtility.HtmlEncode(antiForgeryToken)}\" />";
}
