using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace EfUi.AspNetCore;

internal static class EfUiRequestForgery
{
    private const string TokenFieldName = "__RequestVerificationToken";
    private static readonly ConcurrentDictionary<string, Lazy<IDataProtectionProvider>> Providers = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, IDataProtector> Protectors = new(StringComparer.Ordinal);

    internal static string GetOrCreateRequestToken(HttpContext httpContext, string routePrefix, string? keyDirectory = null)
    {
        _ = httpContext;

        var protector = GetProtector(routePrefix, keyDirectory);
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return protector.Protect(nonce);
    }

    internal static bool ValidateRequest(IReadOnlyDictionary<string, string[]> formValues, HttpContext httpContext, string routePrefix, string? keyDirectory = null)
    {
        _ = httpContext;

        if (!formValues.TryGetValue(TokenFieldName, out var requestTokens))
        {
            return false;
        }

        var requestToken = requestTokens.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return false;
        }

        try
        {
            _ = GetProtector(routePrefix, keyDirectory).Unprotect(requestToken);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static IDataProtector GetProtector(string routePrefix, string? keyDirectory)
    {
        var normalizedRoutePrefix = NormalizeRoutePrefix(routePrefix);
        var providerKey = ResolveKeyDirectory(keyDirectory).FullName;
        var protectorKey = providerKey + "|" + normalizedRoutePrefix;

        return Protectors.GetOrAdd(protectorKey, _ => GetProvider(providerKey).Value.CreateProtector("EfUi.Antiforgery", normalizedRoutePrefix));
    }

    private static Lazy<IDataProtectionProvider> GetProvider(string providerKey)
        => Providers.GetOrAdd(providerKey, key => new Lazy<IDataProtectionProvider>(() => CreateProvider(new DirectoryInfo(key)), LazyThreadSafetyMode.ExecutionAndPublication));

    private static IDataProtectionProvider CreateProvider(DirectoryInfo keyDirectory)
        => DataProtectionProvider.Create(keyDirectory, builder => builder.SetApplicationName("EfUi"));

    private static DirectoryInfo ResolveKeyDirectory(string? keyDirectory)
    {
        var resolvedDirectory = string.IsNullOrWhiteSpace(keyDirectory)
            ? GetDefaultKeyDirectory()
            : new DirectoryInfo(Path.GetFullPath(keyDirectory));

        Directory.CreateDirectory(resolvedDirectory.FullName);
        return resolvedDirectory;
    }

    private static DirectoryInfo GetDefaultKeyDirectory()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.GetTempPath();
        }

        return new DirectoryInfo(Path.Combine(baseDirectory, "EfUi", "AntiforgeryKeys"));
    }

    private static string NormalizeRoutePrefix(string routePrefix)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            return "/";
        }

        var normalized = routePrefix.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }
}
