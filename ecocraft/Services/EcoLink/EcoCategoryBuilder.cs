using ecocraft.Controllers;
using ecocraft.Models;

namespace ecocraft.Services.EcoLink;

public record EcoCategoryItem(string Name, decimal? Price);
public record EcoCategoryResult(string Name, OfferType OfferType, List<EcoCategoryItem> Items);

// Catégories de boutique envoyées au mod : une ou plusieurs catégories de vente et une catégorie d'achat « Acquisition ».
// Prix null quand Eco Gnome n'en a pas ; l'ancien endpoint les remplace lui-même par ses valeurs historiques.
public static class EcoCategoryBuilder
{
    public static List<EcoCategoryResult> Build((List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) items, DataContext dataContext, string filterSkill, GroupBy groupBy, bool readableNames = true)
    {
        var filterSkills = filterSkill == "" ? [] : filterSkill.Split(',').ToList();
        var categories = items.ToSell;

        // The mod sends skill type names (SmithSkill): the store categories get the skill names players read.
        var skillNames = dataContext.UserElements
            .Select(ue => ue.Element.Recipe?.Skill)
            .OfType<Skill>()
            .DistinctBy(s => s.Name)
            .ToDictionary(s => s.Name, s => s.LocalizedName?.en_US is { Length: > 0 } english ? english : s.Name);
        // The legacy endpoint keeps the raw names: renaming would make the stores of older mods create a second category.
        string SkillLabel(string? name) => name is null ? "" : readableNames ? skillNames.GetValueOrDefault(name, name) : name;

        if (filterSkills.Count > 0)
        {
            categories = categories.Where(i =>
                i.GetAssociatedItemsAndSelf().Any(iot =>
                    iot.Elements.Any(e => e.Quantity is not null && e.IsProduct() && filterSkills.Contains(e.Recipe?.Skill?.Name)))
            ).ToList();
        }

        List<EcoCategoryResult> categoriesToSell;

        if (groupBy != GroupBy.None)
        {
            var groupedCategories = groupBy switch
            {
                GroupBy.Margin => categories.GroupBy(i => i.GetCurrentUserPrice(dataContext)?.UserMargin?.Name ?? null),
                GroupBy.Skill => categories.GroupBy(i => i.GetAssociatedItemsAndSelf().SelectMany(iot => iot.Elements).FirstOrDefault(e => e.Recipe?.Skill != null)?.Recipe.Skill?.Name),
                _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null)
            };

            categoriesToSell = groupedCategories
                .Select(m => new EcoCategoryResult(groupBy == GroupBy.Skill ? SkillLabel(m.Key) : m.Key ?? "", OfferType.Sell, m.Select(i => Item(i, dataContext)).ToList()))
                .ToList();
        }
        else
        {
            var name = filterSkills.Count == 0 ? "Production" : readableNames ? string.Join(", ", filterSkills.Select(SkillLabel)) : filterSkill;
            categoriesToSell = [new EcoCategoryResult(name, OfferType.Sell, categories.Select(i => Item(i, dataContext)).ToList())];
        }

        return categoriesToSell.Append(BuildCategoryToBuy(items.ToBuy, dataContext, filterSkills)).ToList();
    }

    static EcoCategoryResult BuildCategoryToBuy(List<ItemOrTag> toBuy, DataContext dataContext, List<string> filterSkills)
    {
        if (filterSkills.Count > 0)
        {
            toBuy = toBuy.Where(i =>
                i.GetAssociatedItemsAndSelf().Any(iot =>
                    iot.Elements.Any(e => e.Quantity is not null && e.IsIngredient() && filterSkills.Contains(e.Recipe?.Skill?.Name)))
            ).ToList();
        }

        return new EcoCategoryResult(
            "Acquisition",
            OfferType.Buy,
            toBuy.SelectMany(t => t.IsTag ? t.GetAssociatedItemsAndSelf() : [t]).Distinct().Select(t => Item(t, dataContext)).ToList());
    }

    static EcoCategoryItem Item(ItemOrTag itemOrTag, DataContext dataContext) => new(itemOrTag.Name, RoundPrice(itemOrTag.GetCurrentUserPrice(dataContext)?.GetMarginPriceOrPrice()));

    public static decimal? RoundPrice(decimal? price) => price is { } p ? Math.Round(p, 2, MidpointRounding.AwayFromZero) : null;
}
