namespace ecocraft.Models;

public enum SupportedLanguage
{
    English,
    Gibberish,
    French,
    Spanish,
    German,
    Korean,
    BrazilianPortuguese,
    SimplifiedChinese,
    Russian,
    Italian,
    Portuguese,
    Hungarian,
    Japanese,
    Norwegian,
    Polish,
    Dutch,
    Romanian,
    Danish,
    Czech,
    Swedish,
    Ukrainian,
    Greek,
    Arabic,
    Vietnamese,
    Turkish
}

public enum LanguageCode
{
    en_US,
    fr,
    es,
    de,
    ko,
    pt_BR,
    zh_Hans,
    ru,
    it,
    pt_PT,
    hu,
    ja,
    nn,
    pl,
    nl,
    ro,
    da,
    cs,
    sv,
    uk,
    el,
    ar_sa,
    vi,
    tr
}

public interface IHasLocalizedName
{
    public LocalizedField LocalizedName { get; set; }
}