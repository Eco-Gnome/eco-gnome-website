using ecocraft.Models;
using System.Text.Json;
using ecocraft.Extensions;

namespace ecocraft.Services;

public partial class LocalizationService(LocalStorageService localStorageService)
{
    [System.Text.RegularExpressions.GeneratedRegex(@"\{[^\}]+\}")]
    private static partial System.Text.RegularExpressions.Regex ReplacerRegexp();

    private const LanguageCode DefaultLanguageCode = LanguageCode.en_US;
    private static readonly Dictionary<LanguageCode, Dictionary<string, object>> AllTranslations = new();
    private static readonly JsonSerializerOptions JsonSerializerOpts = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public LanguageCode CurrentLanguageCode { get; private set; }

    static LocalizationService()
    {
        foreach (LanguageCode languageCode in Enum.GetValues(typeof(LanguageCode)))
        {
            var filePath = Path.Combine(StaticEnvironmentAccessor.WebHostEnvironment!.WebRootPath, "assets", "lang", $"{languageCode}.json");

            if (File.Exists(filePath))
            {
                AllTranslations.Add(languageCode, LoadTranslationFile(filePath));
            }
            else
            {
                Console.WriteLine($"LanguageCode {languageCode} has no translation file.");

                if (languageCode == DefaultLanguageCode)
                {
                    throw new Exception("Can't start the server without the default language file.");
                }
            }
        }
    }

    public async Task SetLanguageAsync(LanguageCode languageCode)
    {
        CurrentLanguageCode = languageCode;
        await localStorageService.AddItem("LanguageCode", CurrentLanguageCode.ToString());
    }

    private static Dictionary<string, object> LoadTranslationFile(string path)
    {
        return ParseJsonElement(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path), JsonSerializerOpts));
    }

    private static Dictionary<string, object> ParseJsonElement(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>();

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Object => ParseJsonElement(property.Value),
                JsonValueKind.String => property.Value.GetString()!,
                JsonValueKind.Number => property.Value.GetDecimal(),
                JsonValueKind.Array => property.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString()).ToArray(),
                _ => property.Value.ToString()
            };
        }

        return dictionary;
    }

    public string GetTranslation(string key, params string[] args)
    {
        AllTranslations.TryGetValue(CurrentLanguageCode, out var translations);
        translations ??= AllTranslations[DefaultLanguageCode];

        if (TryGetTranslation(translations, key, out var value))
        {
            return ReplacePlaceholders(value, args);
        }

        Console.WriteLine($"Missing translation for key: {key}");
        return key;
    }

    private static bool TryGetTranslation(Dictionary<string, object> translations, string key, out string value)
    {
        value = string.Empty;
        var segments = key.Split('.');
        object current = translations;

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            if (current is Dictionary<string, object> dict1 && index < segments.Length - 1 &&
                dict1.TryGetValue($"{segment}.{segments[index + 1]}", out var next1))
            {
                current = next1;
                index++;
            }
            else if (current is Dictionary<string, object> dict2 && dict2.TryGetValue(segment, out var next2))
            {
                current = next2;
            }
            else
            {
                return false;
            }
        }

        if (current is not string strValue) return false;

        value = strValue;
        return true;
    }

    private string ReplacePlaceholders(string template, string[] args)
    {
        var index = 0;

        return ReplacerRegexp().Replace(template, match => index < args.Length ? args[index++] : match.Value);
    }

    public string GetTranslation(IHasLocalizedName? hasLocalizedName)
    {
        if (hasLocalizedName is null) return "BUG_NO_NAME";

        var translation = CurrentLanguageCode switch
        {
            LanguageCode.en_US => hasLocalizedName.LocalizedName.en_US,
            LanguageCode.fr => hasLocalizedName.LocalizedName.fr,
            LanguageCode.es => hasLocalizedName.LocalizedName.es,
            LanguageCode.de => hasLocalizedName.LocalizedName.de,
            LanguageCode.ko => hasLocalizedName.LocalizedName.ko,
            LanguageCode.pt_BR => hasLocalizedName.LocalizedName.pt_BR,
            LanguageCode.zh_Hans => hasLocalizedName.LocalizedName.zh_Hans,
            LanguageCode.ru => hasLocalizedName.LocalizedName.ru,
            LanguageCode.it => hasLocalizedName.LocalizedName.it,
            LanguageCode.pt_PT => hasLocalizedName.LocalizedName.pt_PT,
            LanguageCode.hu => hasLocalizedName.LocalizedName.hu,
            LanguageCode.ja => hasLocalizedName.LocalizedName.ja,
            LanguageCode.nn => hasLocalizedName.LocalizedName.nn,
            LanguageCode.pl => hasLocalizedName.LocalizedName.pl,
            LanguageCode.nl => hasLocalizedName.LocalizedName.nl,
            LanguageCode.ro => hasLocalizedName.LocalizedName.ro,
            LanguageCode.da => hasLocalizedName.LocalizedName.da,
            LanguageCode.cs => hasLocalizedName.LocalizedName.cs,
            LanguageCode.sv => hasLocalizedName.LocalizedName.sv,
            LanguageCode.uk => hasLocalizedName.LocalizedName.uk,
            LanguageCode.el => hasLocalizedName.LocalizedName.el,
            LanguageCode.ar_sa => hasLocalizedName.LocalizedName.ar_sa,
            LanguageCode.vi => hasLocalizedName.LocalizedName.vi,
            LanguageCode.tr => hasLocalizedName.LocalizedName.tr,
            _ => throw new ArgumentException($"Unsupported LanguageCode: {CurrentLanguageCode}")
        };

        if (string.IsNullOrEmpty(translation))
        {
            translation = hasLocalizedName.LocalizedName.en_US;
        }

        return translation;
    }
}
