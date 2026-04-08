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

        if (translations is not null && TryGetTranslation(translations, key, out var value))
        {
            return ReplacePlaceholders(value, args);
        }

        if (TryGetTranslation(AllTranslations[DefaultLanguageCode], key, out var value2))
        {
            return ReplacePlaceholders(value2, args);
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
        return GetTranslation(hasLocalizedName.LocalizedName);
    }

    public string GetTranslation(LocalizedField? localizedField)
    {
        if (localizedField is null) return string.Empty;
        return GetLocalizedFieldTranslation(localizedField);
    }

    private string GetLocalizedFieldTranslation(LocalizedField localizedField)
    {
        var translation = CurrentLanguageCode switch
        {
            LanguageCode.en_US => localizedField.en_US,
            LanguageCode.fr => localizedField.fr,
            LanguageCode.es => localizedField.es,
            LanguageCode.de => localizedField.de,
            LanguageCode.ko => localizedField.ko,
            LanguageCode.pt_BR => localizedField.pt_BR,
            LanguageCode.zh_Hans => localizedField.zh_Hans,
            LanguageCode.ru => localizedField.ru,
            LanguageCode.it => localizedField.it,
            LanguageCode.pt_PT => localizedField.pt_PT,
            LanguageCode.hu => localizedField.hu,
            LanguageCode.ja => localizedField.ja,
            LanguageCode.nn => localizedField.nn,
            LanguageCode.pl => localizedField.pl,
            LanguageCode.nl => localizedField.nl,
            LanguageCode.ro => localizedField.ro,
            LanguageCode.da => localizedField.da,
            LanguageCode.cs => localizedField.cs,
            LanguageCode.sv => localizedField.sv,
            LanguageCode.uk => localizedField.uk,
            LanguageCode.el => localizedField.el,
            LanguageCode.ar_sa => localizedField.ar_sa,
            LanguageCode.vi => localizedField.vi,
            LanguageCode.tr => localizedField.tr,
            _ => throw new ArgumentException($"Unsupported LanguageCode: {CurrentLanguageCode}")
        };

        return string.IsNullOrEmpty(translation) ? localizedField.en_US : translation;
    }
}
