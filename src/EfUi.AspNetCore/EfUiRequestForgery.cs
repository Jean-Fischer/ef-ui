using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace EfUi.AspNetCore;

internal static class EfUiRequestForgery
{
    private const string TokenFieldName = "__RequestVerificationToken";
    private const string CookiePrefix = "__EfUi.Antiforgery.";
    private static readonly ConcurrentDictionary<string, Lazy<IDataProtectionProvider>> Providers = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, IDataProtector> Protectors = new(StringComparer.Ordinal);

    internal static string GetOrCreateRequestToken(HttpContext httpContext, string routePrefix, string? keyDirectory = null)
    {
        var normalizedRoutePrefix = NormalizeRoutePrefix(routePrefix);
        var cookieSecret = GetOrCreateCookieSecret(httpContext, normalizedRoutePrefix);
        var protector = GetProtector(normalizedRoutePrefix, keyDirectory);
        return protector.Protect(JsonSerializer.Serialize(new RequestTokenPayload(normalizedRoutePrefix, cookieSecret)));
    }

    internal static bool ValidateRequest(IReadOnlyDictionary<string, string[]> formValues, HttpContext httpContext, string routePrefix, string? keyDirectory = null)
    {
        var normalizedRoutePrefix = NormalizeRoutePrefix(routePrefix);

        if (!formValues.TryGetValue(TokenFieldName, out var requestTokens))
        {
            return false;
        }

        var requestToken = requestTokens.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return false;
        }

        var cookieSecret = GetCookieSecret(httpContext, normalizedRoutePrefix);
        if (string.IsNullOrWhiteSpace(cookieSecret))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<RequestTokenPayload>(GetProtector(normalizedRoutePrefix, keyDirectory).Unprotect(requestToken));
            return payload is not null
                && string.Equals(payload.RoutePrefix, normalizedRoutePrefix, StringComparison.Ordinal)
                && string.Equals(payload.CookieSecret, cookieSecret, StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IDataProtector GetProtector(string normalizedRoutePrefix, string? keyDirectory)
    {
        var providerKey = ResolveKeyDirectory(keyDirectory).FullName;
        var protectorKey = providerKey + "|" + normalizedRoutePrefix;

        return Protectors.GetOrAdd(protectorKey, _ => GetProvider(providerKey).Value.CreateProtector("EfUi.Antiforgery", normalizedRoutePrefix));
    }

    private static string GetOrCreateCookieSecret(HttpContext httpContext, string normalizedRoutePrefix)
    {
        var cookieName = GetCookieName(normalizedRoutePrefix);
        if (httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieSecret) && !string.IsNullOrWhiteSpace(cookieSecret))
        {
            return cookieSecret;
        }

        cookieSecret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        httpContext.Response.Cookies.Append(cookieName, cookieSecret, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = normalizedRoutePrefix,
            SameSite = SameSiteMode.Strict,
            // Antiforgery cookies must not travel over plain HTTP.
            Secure = true
        });

        return cookieSecret;
    }

    private static string? GetCookieSecret(HttpContext httpContext, string normalizedRoutePrefix)
    {
        var cookieName = GetCookieName(normalizedRoutePrefix);
        return httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieSecret)
            ? cookieSecret
            : null;
    }

    private static string GetCookieName(string normalizedRoutePrefix)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRoutePrefix))).ToLowerInvariant();
        return CookiePrefix + hash;
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
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new InvalidOperationException("Could not resolve a private application data directory for antiforgery keys.");
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

    private sealed record RequestTokenPayload(string RoutePrefix, string CookieSecret);
}
