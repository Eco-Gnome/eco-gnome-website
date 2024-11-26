using ecocraft.Models;
using System.Text.Json;

namespace ecocraft.Services;

public class LocalizationService(
    LocalStorageService localStorageService,
    IWebHostEnvironment env)
{
    public static LanguageCode CurrentLanguageCode { get; private set; }
    private Dictionary<string, string> _translations = new();
    private Dictionary<string, string> _translations_enUS = new();

    public async Task SetLanguageAsync(LanguageCode languageCode)
    {
        CurrentLanguageCode = languageCode;
        var filePath = Path.Combine(env.WebRootPath, "assets", "lang", $"{languageCode}.json");
        var filePath_enUS = Path.Combine(env.WebRootPath, "assets", "lang", "en_US.json");

        if (File.Exists(filePath) && File.Exists(filePath_enUS))
        {
            var json = await File.ReadAllTextAsync(filePath);
            _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

            json = await File.ReadAllTextAsync(filePath_enUS);
            _translations_enUS = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        else if (File.Exists(filePath_enUS))
        {
            Console.WriteLine($"Translation file for '{languageCode}' not found at {filePath}. Fallback on en_US.json.");
            var json = await File.ReadAllTextAsync(filePath_enUS);
            _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            _translations_enUS = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        else
        {
            Console.WriteLine($"Translation file for '{languageCode}' not found at {filePath}. Fallback on en_US.json isn't working.");
            _translations = new();
            _translations_enUS = new();
        }


        await localStorageService.AddItem("LanguageCode", CurrentLanguageCode.ToString());
    }

    public string GetTranslation(string key)
    {
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }else if(_translations_enUS.TryGetValue(key, out var value_enUS))
        {
            return value_enUS;
        }

        Console.WriteLine($"Missing translation for key: {key}");

        return $"{key}";
    }

    public static string GetTranslation(IHasLocalizedName? hasLocalizedName)
    {
        if (hasLocalizedName is null) return "BUG_NO_NAME";

        string translation;

        switch (CurrentLanguageCode)
        {
            case LanguageCode.en_US:
                translation = hasLocalizedName.LocalizedName.en_US;
                break;
            case LanguageCode.fr:
                translation = hasLocalizedName.LocalizedName.fr;
                break;
            case LanguageCode.es:
                translation = hasLocalizedName.LocalizedName.es;
                break;
            case LanguageCode.de:
                translation = hasLocalizedName.LocalizedName.de;
                break;
            case LanguageCode.ko:
                translation = hasLocalizedName.LocalizedName.ko;
                break;
            case LanguageCode.pt_BR:
                translation = hasLocalizedName.LocalizedName.pt_BR;
                break;
            case LanguageCode.zh_Hans:
                translation = hasLocalizedName.LocalizedName.zh_Hans;
                break;
            case LanguageCode.ru:
                translation = hasLocalizedName.LocalizedName.ru;
                break;
            case LanguageCode.it:
                translation = hasLocalizedName.LocalizedName.it;
                break;
            case LanguageCode.pt_PT:
                translation = hasLocalizedName.LocalizedName.pt_PT;
                break;
            case LanguageCode.hu:
                translation = hasLocalizedName.LocalizedName.hu;
                break;
            case LanguageCode.ja:
                translation = hasLocalizedName.LocalizedName.ja;
                break;
            case LanguageCode.nn:
                translation = hasLocalizedName.LocalizedName.nn;
                break;
            case LanguageCode.pl:
                translation = hasLocalizedName.LocalizedName.pl;
                break;
            case LanguageCode.nl:
                translation = hasLocalizedName.LocalizedName.nl;
                break;
            case LanguageCode.ro:
                translation = hasLocalizedName.LocalizedName.ro;
                break;
            case LanguageCode.da:
                translation = hasLocalizedName.LocalizedName.da;
                break;
            case LanguageCode.cs:
                translation = hasLocalizedName.LocalizedName.cs;
                break;
            case LanguageCode.sv:
                translation = hasLocalizedName.LocalizedName.sv;
                break;
            case LanguageCode.uk:
                translation = hasLocalizedName.LocalizedName.uk;
                break;
            case LanguageCode.el:
                translation = hasLocalizedName.LocalizedName.el;
                break;
            case LanguageCode.ar_sa:
                translation = hasLocalizedName.LocalizedName.ar_sa;
                break;
            case LanguageCode.vi:
                translation = hasLocalizedName.LocalizedName.vi;
                break;
            case LanguageCode.tr:
                translation = hasLocalizedName.LocalizedName.tr;
                break;
            default:
                throw new ArgumentException($"Unsupported LanguageCode: {CurrentLanguageCode}");
        }

        if (string.IsNullOrEmpty(translation))
        {
            translation = hasLocalizedName.LocalizedName.en_US;
        }

        return translation;
    }

}
