using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BeDemo.Api.Localization;
using BeDemo.Api.Localization.Admin;
using BeDemo.Api.Localization.Mobile;
using BeDemo.Api.Localization.Portal;
using Microsoft.Extensions.Caching.Memory;

namespace BeDemo.Api.Services;

public interface ILocalizationBundleService
{
    LocalizationBundleResponse? GetBundle(LocalizationApp app);
}

/// <summary>
/// Loads embedded .resx satellites, unflattens to nested JSON for i18next, caches per app.
/// API language code <c>cz</c> maps to .NET culture <c>cs</c>.
/// </summary>
public sealed class LocalizationBundleService : ILocalizationBundleService
{
    private static readonly string[] ApiLanguages = ["en", "sk", "cz"];
    private static readonly IReadOnlyDictionary<string, CultureInfo> CultureByApiLanguage =
        new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = CultureInfo.GetCultureInfo("en"),
            ["sk"] = CultureInfo.GetCultureInfo("sk"),
            ["cz"] = CultureInfo.GetCultureInfo("cs"),
        };

    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalizationBundleService> _logger;

    public LocalizationBundleService(IMemoryCache cache, ILogger<LocalizationBundleService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public LocalizationBundleResponse? GetBundle(LocalizationApp app)
    {
        return _cache.GetOrCreate($"localization-bundle:{app}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return BuildBundle(app);
        });
    }

    private LocalizationBundleResponse? BuildBundle(LocalizationApp app)
    {
        var (resourceType, appName, defaultNs, isMobile) = app switch
        {
            LocalizationApp.Portal => (typeof(PortalResources), "portal", "common", false),
            LocalizationApp.Admin => (typeof(AdminResources), "admin", "common", false),
            LocalizationApp.Mobile => (typeof(MobileResources), "mobile", "common", true),
            _ => throw new ArgumentOutOfRangeException(nameof(app)),
        };

        var rm = new ResourceManager(resourceType.FullName!, resourceType.Assembly);
        var resources = new Dictionary<string, Dictionary<string, JsonObject>>(StringComparer.Ordinal);
        var hashInput = new StringBuilder();

        foreach (var apiLang in ApiLanguages)
        {
            var culture = CultureByApiLanguage[apiLang];
            var flat = ReadAllStrings(rm, culture);
            hashInput.Append(apiLang).Append('\0');
            foreach (var kv in flat.OrderBy(k => k.Key, StringComparer.Ordinal))
                hashInput.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');

            if (isMobile)
            {
                var nsObjects = ResourceJsonUnflattener.ToMobileNamespaces(flat);
                resources[apiLang] = nsObjects.ToDictionary(
                    x => x.Key,
                    x => x.Value,
                    StringComparer.Ordinal);
            }
            else
            {
                resources[apiLang] = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
                {
                    [defaultNs] = ResourceJsonUnflattener.ToNestedObject(flat),
                };
            }
        }

        if (resources.Values.All(lang => lang.Count == 0 || lang.Values.All(o => o.Count == 0)))
        {
            _logger.LogWarning("Localization bundle for {App} is empty", appName);
            return null;
        }

        var version = ComputeHash(hashInput.ToString());
        return new LocalizationBundleResponse
        {
            App = appName,
            Version = version,
            DefaultNamespace = defaultNs,
            SupportedLanguages = ApiLanguages.ToList(),
            Resources = resources,
        };
    }

    private static Dictionary<string, string> ReadAllStrings(ResourceManager rm, CultureInfo culture)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var set = rm.GetResourceSet(culture, true, true);
        if (set == null)
            return result;

        foreach (System.Collections.DictionaryEntry entry in set)
        {
            if (entry.Key is string key && entry.Value is string value)
                result[key] = value;
        }

        return result;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
