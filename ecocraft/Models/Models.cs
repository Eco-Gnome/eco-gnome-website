using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ecocraft.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace ecocraft.Models;

public interface IHasIconName
{
    public string Name { get; set; }
}

public enum MarginType
{
    MarkUp,
    GrossMargin,
}

public enum CalculationMode
{
    AutoSmart,
    Manual,
}

public enum RoundingMode
{
    None,
    Up,
    Down,
    Marketing,
}

public interface ISLinkedToModifier;

// Eco Data
public class Recipe: IHasLocalizedName
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public string FamilyName { get; set; }
    [ForeignKey("DynamicValue")] public Guid CraftMinutesId { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public long SkillLevel { get; set; }
    public bool IsBlueprint { get; set; }
    public bool IsDefault { get; set; }
    [ForeignKey("DynamicValue")] public Guid LaborId { get; set; }
    [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }
    public bool IsShareLocked { get; set; } = false;

    public LocalizedField LocalizedName { get; set; }
    public DynamicValue CraftMinutes { get; set; }
    public Skill? Skill { get; set; }
    public DynamicValue Labor { get; set; }
    public CraftingTable CraftingTable { get; set; }
    public Server Server { get; set; }
    public List<Element> Elements { get; set; } = [];
    public List<UserRecipe> UserRecipes { get; set; } = [];

    public UserRecipe? GetCurrentUserRecipe(DataContext dataContext)
    {
        return dataContext.UserRecipes.FirstOrDefault(ur => ur.RecipeId == Id);
    }

    /// <summary>
    /// Effective craft time in minutes. When the craft time is driven by a world Layer (e.g. Oilfield for
    /// Petroleum), its real value cannot be computed from the exported data, so the user's override is used
    /// when set. Otherwise the standard dynamic value applies.
    /// </summary>
    public decimal GetEffectiveCraftMinutes(DataContext dataContext, UserRecipe? userRecipe, DynamicValueCalculationContext? calculationContext = null)
    {
        if (CraftMinutes.HasLayerModifier && userRecipe?.CraftMinutesOverride is { } craftMinutesOverride)
        {
            return craftMinutesOverride;
        }

        return CraftMinutes.GetDynamicValue(dataContext, calculationContext);
    }

    public override string ToString()
    {
        return Name;
    }
}

public class Element
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Recipe")] public Guid RecipeId { get; set; }
    [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }
    public int Index { get; set; }
    [ForeignKey("DynamicValue")] public Guid QuantityId { get; set; }
    public bool DefaultIsReintegrated { get; set; }
    public decimal DefaultShare { get; set; }

    public Recipe Recipe { get; set; }
    public ItemOrTag ItemOrTag { get; set; }
    public DynamicValue Quantity { get; set; }
    public Skill? Skill { get; set; }
    public List<UserElement> UserElements { get; set; } = [];

    public UserElement GetMandatoryCurrentUserElement(DataContext dataContext)
    {
        return dataContext.UserElements.First(ur => ur.ElementId == Id);
    }

    public UserElement? GetCurrentUserElement(DataContext dataContext)
    {
        return dataContext.UserElements.FirstOrDefault(ur => ur.ElementId == Id);
    }

    public bool IsIngredient()
    {
        return Quantity.BaseValue < 0;
    }

    public bool IsProduct()
    {
        return Quantity.BaseValue > 0;
    }

    public override string ToString()
    {
        return ItemOrTag.Name;
    }

    public decimal GetDynamicQuantity(DataContext dataContext)
    {
        return Quantity.GetDynamicValue(dataContext);
    }
}

public class DynamicValue
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public decimal BaseValue { get; set; }
    public bool HasLayerModifier { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public List<Modifier> Modifiers { get; set; } = [];
    public List<Recipe> LaborRecipes { get; set; } = [];
    public List<Recipe> CraftMinutesRecipes { get; set; } = [];
    public List<Element> QuantityElements { get; set; } = [];
    public Recipe? Recipe => LaborRecipes.FirstOrDefault() ?? CraftMinutesRecipes.FirstOrDefault();
    public Element? Element => QuantityElements.FirstOrDefault();
    public Server Server { get; set; }

    public bool IsDynamic()
    {
        return Modifiers.Count > 0;
    }

    public bool HasDynamicEffect(DataContext dataContext)
    {
        return GetDynamicValue(dataContext) != BaseValue;
    }

    public TalentBonusAction? GetTalentAction()
    {
        if (LaborRecipes.Count > 0) return TalentBonusAction.LaborCost;
        if (CraftMinutesRecipes.Count > 0) return TalentBonusAction.CraftTime;
        var element = QuantityElements.FirstOrDefault();
        if (element is null) return null;
        return element.IsIngredient() ? TalentBonusAction.ResourceCost : TalentBonusAction.Yield;
    }

    private Recipe? GetOwningRecipe()
    {
        return Recipe ?? Element?.Recipe;
    }

    // Names of the tags carried by the recipe's products — the target of the bonus ItemTags filters.
    private List<string> GetRecipeProductTags()
    {
        var recipe = GetOwningRecipe();
        if (recipe is null) return [];

        return recipe.Elements
            .Where(e => e.IsProduct() && (object?)e.ItemOrTag is not null)
            .SelectMany(e => e.ItemOrTag.AssociatedTags)
            .Select(t => t.Name)
            .Distinct()
            .ToList();
    }

    private IEnumerable<TalentBonus> GetMatchingBonuses(Modifier modifier, TalentBonusAction? action, Skill? recipeSkill, Lazy<List<string>> recipeProductTags)
    {
        var talent = modifier.Talent;
        if (talent is null) return [];
        if (action is null) return [];
        return talent.Bonuses.Where(b => b.Action == action.Value && b.PassesFilters(recipeSkill, recipeProductTags));
    }

    // Whether the installed modules' bonuses reach this value (v4 rules): LaborCost and CraftTime
    // always, ResourceCost only on non-static ingredients, Yield only on non-refund products.
    private bool ModuleBonusesApply(TalentBonusAction action)
    {
        return action switch
        {
            TalentBonusAction.LaborCost => true,
            TalentBonusAction.CraftTime => true,
            TalentBonusAction.ResourceCost => IsDynamic(),
            TalentBonusAction.Yield => Element?.DefaultIsReintegrated != true,
            _ => false,
        };
    }

    private IEnumerable<(PluginModule PluginModule, TalentBonus Bonus)> GetApplicableModuleBonuses(
        DataContext dataContext,
        DynamicValueCalculationContext? calculationContext,
        TalentBonusAction? action,
        Recipe? recipe,
        Lazy<List<string>> recipeProductTags)
    {
        if (action is null || recipe is null || !ModuleBonusesApply(action.Value)) yield break;

        var userCraftingTable = calculationContext is not null
            ? calculationContext.GetUserCraftingTable(recipe, dataContext)
            : recipe.CraftingTable.GetCurrentUserCraftingTable(dataContext);
        if (userCraftingTable is null) yield break;

        foreach (var pluginModule in userCraftingTable.PluginModules)
        {
            foreach (var bonus in pluginModule.Bonuses)
            {
                if (bonus.Action == action.Value && bonus.PassesFilters(recipe.Skill, recipeProductTags))
                {
                    yield return (pluginModule, bonus);
                }
            }
        }
    }

    private readonly record struct BonusAggregate(decimal Multiplier, decimal Additive, decimal PercentSum, decimal ChanceAdditive, decimal? OverrideValue);

    // Collects every applicable bonus (skill labor reduction + user talents via the exported
    // modifiers + the modules installed on the recipe's crafting table) into the §3 components.
    private BonusAggregate CollectBonuses(DataContext dataContext, DynamicValueCalculationContext? calculationContext)
    {
        var multiplier = 1m;
        var additive = 0m;
        var percentSum = 0m;
        var chanceAdditive = 0m;
        decimal? overrideValue = null;

        var action = GetTalentAction();
        var recipe = GetOwningRecipe();
        // Lazy: walking the products' tags is only needed when a bonus carries ItemTags filters.
        var recipeProductTags = new Lazy<List<string>>(GetRecipeProductTags);

        void ApplyBonus(TalentBonus bonus, int level)
        {
            switch (bonus.EffectType)
            {
                case TalentBonusEffectType.Multiplicative:
                case TalentBonusEffectType.CappedMultiplicative:
                case TalentBonusEffectType.TieredMultiplicative:
                    multiplier *= Talent.GetBonusMultiplier(bonus, level);
                    break;
                case TalentBonusEffectType.Additive:
                    additive += BaseValue < 0 ? -bonus.Value : bonus.Value;
                    break;
                case TalentBonusEffectType.AdditivePercent:
                    // Pooled additively across all sources, applied once against the base value.
                    percentSum += bonus.Value;
                    break;
                case TalentBonusEffectType.Chance:
                    // Average-price calculator: use the expected value.
                    chanceAdditive += (bonus.Chance ?? 0m) * (BaseValue < 0 ? -bonus.Value : bonus.Value);
                    break;
                case TalentBonusEffectType.Override:
                    overrideValue = BaseValue < 0 ? -bonus.Value : bonus.Value;
                    break;
            }
        }

        foreach (var modifier in Modifiers)
        {
            switch (modifier.DynamicType)
            {
                case "Talent":
                {
                    var userTalent = modifier.TalentId is not null
                        ? calculationContext is not null
                            ? calculationContext.GetUserTalent(modifier.TalentId.Value, dataContext)
                            : dataContext.UserTalents.FirstOrDefault(ut => ut.TalentId == modifier.TalentId.Value)
                        : null;
                    if (userTalent is null) break;

                    foreach (var bonus in GetMatchingBonuses(modifier, action, recipe?.Skill, recipeProductTags))
                    {
                        ApplyBonus(bonus, userTalent.Level);
                    }
                    break;
                }
                case "Skill":
                {
                    var userSkill = modifier.Skill is not null
                        ? calculationContext is not null
                            ? calculationContext.GetUserSkill(modifier.Skill, dataContext)
                            : modifier.Skill.GetCurrentUserSkill(dataContext)
                        : null;
                    multiplier *= modifier.Skill is not null && userSkill is not null
                        ? modifier.Skill.GetLevelLaborReducePercent(userSkill.Level)
                        : 1m;
                    break;
                }
                // "Module" modifiers no longer drive any math (v4): module effects come exclusively
                // from the installed modules' bonuses, handled below.
            }
        }

        foreach (var (_, bonus) in GetApplicableModuleBonuses(dataContext, calculationContext, action, recipe, recipeProductTags))
        {
            ApplyBonus(bonus, 1);
        }

        return new BonusAggregate(multiplier, additive, percentSum, chanceAdditive, overrideValue);
    }

    public decimal GetBaseValue()
    {
        return BaseValue;
    }

    public decimal GetRoundFactorBaseValue(DataContext dataContext, DynamicValueCalculationContext? calculationContext = null)
    {
        var roundFactor = GetRoundFactor(dataContext, calculationContext);

        return ApplyRoundFactor(BaseValue, roundFactor);
    }

    public decimal GetDynamicValue(DataContext dataContext, DynamicValueCalculationContext? calculationContext = null)
    {
        var dynamicValueCache = calculationContext?.DynamicValueCache;
        if (dynamicValueCache is not null && dynamicValueCache.TryGetValue(Id, out var cachedValue))
        {
            return cachedValue;
        }

        // §3 effect math: multiplicatives compound, flat additives, then Override, then the
        // AdditivePercent pool applied once against the pre-bonus base, then Chance (expected value).
        var aggregate = CollectBonuses(dataContext, calculationContext);
        var dynamicValue = BaseValue * aggregate.Multiplier + aggregate.Additive;
        if (aggregate.OverrideValue is not null)
        {
            dynamicValue = aggregate.OverrideValue.Value;
        }
        dynamicValue += BaseValue * aggregate.PercentSum;
        dynamicValue += aggregate.ChanceAdditive;

        if (dynamicValueCache is not null)
        {
            dynamicValueCache[Id] = dynamicValue;
        }

        return dynamicValue;
    }

    public decimal GetRoundFactorDynamicValue(DataContext dataContext, DynamicValueCalculationContext? calculationContext = null)
    {
        var roundDynamicValueCache = calculationContext?.RoundDynamicValueCache;
        if (roundDynamicValueCache is not null && roundDynamicValueCache.TryGetValue(Id, out var cachedValue))
        {
            return cachedValue;
        }

        var roundFactor = GetRoundFactor(dataContext, calculationContext);
        var dynamicValue = GetDynamicValue(dataContext, calculationContext);
        var roundDynamicValue = ApplyRoundFactor(dynamicValue, roundFactor);

        if (roundDynamicValueCache is not null)
        {
            roundDynamicValueCache[Id] = roundDynamicValue;
        }

        return roundDynamicValue;
    }

    private int GetRoundFactor(DataContext dataContext, DynamicValueCalculationContext? calculationContext)
    {
        var recipe = Recipe ?? Element?.Recipe;

        if (calculationContext is not null)
        {
            return recipe is not null ? calculationContext.GetRoundFactor(recipe, dataContext) : 0;
        }

        return recipe!.GetCurrentUserRecipe(dataContext)!.RoundFactor;
    }

    internal static decimal ApplyRoundFactor(decimal value, int roundFactor)
    {
        if (roundFactor == 0) return value;

        return value < 0
            ? Math.Floor(value * roundFactor) / roundFactor
            : Math.Ceiling(value * roundFactor) / roundFactor;
    }

    public string GetMultiplierTooltip(DataContext dataContext, LocalizationService localizationService, string? baseValue = null)
    {
        baseValue ??= Math.Abs(Math.Round(GetBaseValue(), 0, MidpointRounding.AwayFromZero)).ToString();

        List<string> tooltip = [];

        var action = GetTalentAction();
        var recipe = GetOwningRecipe();
        var recipeProductTags = new Lazy<List<string>>(GetRecipeProductTags);

        string FormatAdditive(decimal value)
        {
            var addedValue = BaseValue < 0 ? -value : value;
            var rounded = Math.Round(addedValue, 2, MidpointRounding.AwayFromZero);
            return (rounded > 0 ? "+" : "") + rounded.ToString("0.##");
        }

        static string FormatReductionPercent(decimal multiplier)
        {
            return Math.Round(100 - multiplier * 100, 1, MidpointRounding.AwayFromZero).ToString("0.##");
        }

        // Single formatter for one bonus line, shared by the talent and module loops so the
        // per-effect-type display rules cannot drift between the two sources.
        void AddBonusTooltip(TalentBonus bonus, int level, string sourceName, string reductionKey, string additiveKey)
        {
            switch (bonus.EffectType)
            {
                case TalentBonusEffectType.Multiplicative:
                case TalentBonusEffectType.CappedMultiplicative:
                case TalentBonusEffectType.TieredMultiplicative:
                {
                    var bonusMultiplier = Talent.GetBonusMultiplier(bonus, level);
                    if (bonusMultiplier == 1m) break;
                    tooltip.Add(localizationService.GetTranslation(reductionKey, sourceName, FormatReductionPercent(bonusMultiplier)));
                    break;
                }
                case TalentBonusEffectType.AdditivePercent:
                {
                    if (bonus.Value == 0m) break;
                    tooltip.Add(localizationService.GetTranslation(reductionKey, sourceName, Math.Round(-bonus.Value * 100, 1, MidpointRounding.AwayFromZero).ToString("0.##")));
                    break;
                }
                case TalentBonusEffectType.Additive:
                {
                    if (bonus.Value == 0m) break;
                    tooltip.Add(localizationService.GetTranslation(additiveKey, sourceName, FormatAdditive(bonus.Value)));
                    break;
                }
                case TalentBonusEffectType.Chance:
                {
                    var expected = (bonus.Chance ?? 0m) * bonus.Value;
                    if (expected == 0m) break;
                    tooltip.Add(localizationService.GetTranslation(additiveKey, sourceName, FormatAdditive(expected)));
                    break;
                }
            }
        }

        foreach (var modifier in Modifiers)
        {
            switch (modifier.DynamicType)
            {
                case "Talent":
                {
                    var userTalent = modifier.TalentId is not null
                        ? dataContext.UserTalents.FirstOrDefault(ut => ut.TalentId == modifier.TalentId.Value)
                        : null;
                    var talent = modifier.Talent ?? userTalent?.Talent;
                    if (userTalent is null || talent is null) break;

                    foreach (var bonus in GetMatchingBonuses(modifier, action, recipe?.Skill, recipeProductTags))
                    {
                        AddBonusTooltip(bonus, userTalent.Level, localizationService.GetTranslation(talent), "RecipeDialog.TalentReductionTooltip", "RecipeDialog.TalentAdditiveTooltip");
                    }
                    break;
                }
                case "Skill":
                {
                    var multiplier = modifier.Skill?.GetCurrentUserSkill(dataContext) is not null ? modifier.Skill.GetLevelLaborReducePercent(modifier.Skill.GetCurrentUserSkill(dataContext)!.Level) : 1m;

                    if (multiplier != 1m)
                    {
                        tooltip.Add(localizationService.GetTranslation(
                            "RecipeDialog.SkillReductionTooltip",
                            localizationService.GetTranslation(modifier.Skill),
                            modifier.Skill!.GetCurrentUserSkill(dataContext)!.Level.ToString(),
                            FormatReductionPercent(multiplier)
                        ));
                    }
                    break;
                }
            }
        }

        // Installed modules (one per slot, all active): each applicable bonus gets its own line.
        // Module bonuses are not level-scaled, so they are described at level 1.
        foreach (var (pluginModule, bonus) in GetApplicableModuleBonuses(dataContext, null, action, recipe, recipeProductTags))
        {
            AddBonusTooltip(bonus, 1, localizationService.GetTranslation(pluginModule), "RecipeDialog.ModuleReductionTooltip", "RecipeDialog.ModuleAdditiveTooltip");
        }

        var dynamicValue = GetDynamicValue(dataContext);

        if (dynamicValue != BaseValue && BaseValue != 0m)
        {
            var prefix = localizationService.GetTranslation(
                "RecipeDialog.TotalReductionTooltip",
                FormatReductionPercent(dynamicValue / BaseValue)
            );
            return baseValue + " " + prefix + string.Join(", ", tooltip);
        }

        return string.Join(", ", tooltip);
    }
}

/*
ValueType in ECO is:
    Efficiency,
    Speed,
    CalorieReduction,
    Damage,
    Yield,
    Misc,
    LaborEfficiency
 */

public class Modifier
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string DynamicType { get; set; }
    public string ValueType { get; set; } = "";
    [ForeignKey("DynamicValue")] public Guid DynamicValueId { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    [ForeignKey("Talent")] public Guid? TalentId { get; set; }

    public DynamicValue DynamicValue { get; set; }
    public Skill? Skill { get; set; }
    public Talent? Talent { get; set; }
}

public class ItemOrTag: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public bool IsTag { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? FuelCalories { get; set; }
    public decimal? FuelConsumptionPerSecond { get; set; }
    public string[]? AcceptedFuelTags { get; set; }
    public decimal? FoodCalories { get; set; }
    public decimal? FoodCarbs { get; set; }
    public decimal? FoodProtein { get; set; }
    public decimal? FoodFat { get; set; }
    public decimal? FoodVitamins { get; set; }
    public string? HousingRoomCategory { get; set; }
    public decimal? HousingBaseValue { get; set; }
    public string? HousingTypeForRoomLimit { get; set; }
    public decimal? HousingDiminishingReturnMultiplier { get; set; }
    public decimal? HousingDiminishingMultiplierAcrossFullProperty { get; set; }
    public decimal? RoomMaterialTier { get; set; }
    public decimal? RoomVolume { get; set; }
    public bool RoomRequiresContainment { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<Element> Elements { get; set; } = [];
    public List<UserPrice> UserPrices { get; set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; set; } = [];
    public List<ItemOrTag> AssociatedTags { get; set; } = [];
    public List<ItemOrTag> AssociatedItems { get; set; } = [];

    public UserPrice? GetCurrentUserPrice(DataContext dataContext)
    {
        return UserPrices.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
    }

    public UserPrice GetMandatoryCurrentUserPrice(DataContext dataContext)
    {
        var userPrice = UserPrices.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
        if (userPrice is null)
        {
            throw new Exception(this.ToString());
        }

        return userPrice;
    }

    public List<ItemOrTag> GetAssociatedItemsAndSelf()
    {
        return AssociatedItems.Prepend(this).ToList();
    }

    public List<ItemOrTag> GetAssociatedTagsAndSelf()
    {
        return AssociatedTags.Prepend(this).ToList();
    }

    public override string ToString()
    {
        return Name;
    }
}

public class Skill: IHasLocalizedName, IHasIconName, ISLinkedToModifier
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public string? Profession { get; set; }
    public int MaxLevel { get; set; }
    public decimal[] LaborReducePercent { get; set; }

    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<Recipe> Recipes { get; set; } = [];
    public List<UserSkill> UserSkills { get; set; } = [];
    public List<Talent> Talents { get; set; } = [];
    public List<Modifier> Modifiers { get; set; } = [];

    public UserSkill? GetCurrentUserSkill(DataContext dataContext)
    {
        return dataContext.UserSkills.FirstOrDefault(ur => ur.SkillId == Id);
    }

    public override string ToString()
    {
        return Name;
    }

    public decimal GetLevelLaborReducePercent(int level)
    {
        return level >= LaborReducePercent.Length ? LaborReducePercent.Last() : LaborReducePercent[level];
    }
}

public enum TalentBonusAction
{
    ResourceCost = 0,
    LaborCost = 1,
    CraftTime = 2,
    Yield = 3,
}

public enum TalentBonusEffectType
{
    Multiplicative = 0,
    CappedMultiplicative = 1,
    Additive = 2,
    Override = 3,
    AdditivePercent = 4,
    Chance = 5,
    TieredMultiplicative = 6,
}

// Shared bonus shape: carried by a Talent (TalentId set) or by a PluginModule (PluginModuleId set).
public class TalentBonus
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Talent")] public Guid? TalentId { get; set; }
    [ForeignKey("PluginModule")] public Guid? PluginModuleId { get; set; }
    public TalentBonusAction Action { get; set; }
    public TalentBonusEffectType EffectType { get; set; }
    public decimal Value { get; set; }
    public decimal? Cap { get; set; }
    public decimal? Chance { get; set; }
    public decimal[]? Levels { get; set; }
    public string[]? SkillTypes { get; set; }
    public string[]? ExcludedSkillTypes { get; set; }
    public string[]? ItemTags { get; set; }

    public Talent? Talent { get; set; }
    public PluginModule? PluginModule { get; set; }

    // A bonus only applies to a recipe when its skill/tag filters match the recipe's required
    // skill and the tags carried by at least one of the recipe's products. The product tags come
    // in lazily: most bonuses carry no ItemTags filter, so the tag walk is usually skipped.
    public bool PassesFilters(Skill? recipeSkill, Lazy<List<string>> recipeProductTags)
    {
        if (SkillTypes is { Length: > 0 } && (recipeSkill is null || !SkillTypes.Contains(recipeSkill.Name))) return false;
        if (ExcludedSkillTypes is { Length: > 0 } && recipeSkill is not null && ExcludedSkillTypes.Contains(recipeSkill.Name)) return false;
        if (ItemTags is { Length: > 0 } && !ItemTags.Any(recipeProductTags.Value.Contains)) return false;

        return true;
    }

    // Compact human-readable form, e.g. "Resource cost -10%" or "Yield +1", used in module tooltips
    // and the data editor. Level-scaled effects are described at level 1.
    public string GetDescription(LocalizationService localizationService)
    {
        var actionLabel = localizationService.GetTranslation($"Bonus.Action.{Action}");

        switch (EffectType)
        {
            case TalentBonusEffectType.Multiplicative:
            case TalentBonusEffectType.CappedMultiplicative:
            case TalentBonusEffectType.TieredMultiplicative:
            {
                var percent = (Talent.GetBonusMultiplier(this, 1) - 1m) * 100;
                return percent == 0m ? "" : $"{actionLabel} {FormatPercent(percent)}";
            }
            case TalentBonusEffectType.AdditivePercent:
                return Value == 0m ? "" : $"{actionLabel} {FormatPercent(Value * 100)}";
            case TalentBonusEffectType.Additive:
                return $"{actionLabel} {(Value > 0 ? "+" : "")}{Value:0.##}";
            case TalentBonusEffectType.Override:
                return $"{actionLabel} = {Value:0.##}";
            case TalentBonusEffectType.Chance:
                return $"{actionLabel} {(Value > 0 ? "+" : "")}{Value:0.##} ({(Chance ?? 0) * 100:0.##}%)";
            default:
                return "";
        }
    }

    private static string FormatPercent(decimal percent)
    {
        return (percent > 0 ? "+" : "−") + Math.Abs(Math.Round(percent, 1, MidpointRounding.AwayFromZero)).ToString("0.##") + "%";
    }
}

public class Talent: IHasLocalizedName, ISLinkedToModifier, IHasIconName
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    [ForeignKey("LocalizedDescription")] public Guid? LocalizedDescriptionId { get; set; }
    public string TalentGroupName { get; set; }
    public int Level { get; set; }
    public int MaxLevel { get; set; }
    [ForeignKey("Skill")] public Guid SkillId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public LocalizedField LocalizedDescription { get; set; }
    public Skill Skill { get; set; }
    public List<Modifier> Modifiers { get; set; } = [];
    public List<UserTalent> UserTalents { get; set; } = [];
    public List<TalentBonus> Bonuses { get; set; } = [];

    public UserTalent? GetCurrentUserTalent(DataContext dataContext)
    {
        return dataContext.UserTalents.FirstOrDefault(ur => ur.TalentId == Id);
    }

    public static decimal GetBonusMultiplier(TalentBonus bonus, int level)
    {
        switch (bonus.EffectType)
        {
            case TalentBonusEffectType.Multiplicative:
                return bonus.Value;
            case TalentBonusEffectType.CappedMultiplicative:
            {
                var multiplier = 1m + (bonus.Value - 1m) * level;
                if (bonus.Cap is null) return multiplier;
                if (bonus.Value < 1m) return Math.Max(multiplier, bonus.Cap.Value);
                if (bonus.Value > 1m) return Math.Min(multiplier, bonus.Cap.Value);
                return multiplier;
            }
            case TalentBonusEffectType.TieredMultiplicative:
            {
                if (bonus.Levels is not { Length: > 0 }) return bonus.Value;
                return bonus.Levels[Math.Clamp(level, 1, bonus.Levels.Length) - 1];
            }
            default:
                return 1m;
        }
    }

    private TalentBonus? GetReductionBonus()
    {
        return Bonuses.FirstOrDefault(b =>
            b.EffectType is TalentBonusEffectType.CappedMultiplicative or TalentBonusEffectType.TieredMultiplicative
            || (b.EffectType == TalentBonusEffectType.Multiplicative && b.Value != 1m && b.Value != 0m)
            || (b.EffectType == TalentBonusEffectType.AdditivePercent && b.Value != 0m));
    }

    public bool HasReductionDisplay()
    {
        return GetReductionBonus() is not null;
    }

    public decimal GetReductionPercentForLevel(int level)
    {
        var bonus = GetReductionBonus();
        if (bonus is null) return 0m;

        if (bonus.EffectType == TalentBonusEffectType.AdditivePercent)
        {
            return Math.Round(-bonus.Value * 100, 1, MidpointRounding.AwayFromZero);
        }

        return Math.Round((1 - GetBonusMultiplier(bonus, level)) * 100, 1, MidpointRounding.AwayFromZero);
    }
}

public class CraftingTable: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }
    public decimal? RoomMaterialTier { get; set; }
    public decimal? RoomVolume { get; set; }
    public bool RoomRequiresContainment { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<UserCraftingTable> UserCraftingTables { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];
    public List<ModuleSlot> ModuleSlots { get; set; } = [];

    public UserCraftingTable? GetCurrentUserCraftingTable(DataContext dataContext)
    {
        return dataContext.UserCraftingTables.FirstOrDefault(ur => ur.CraftingTableId == Id);
    }

    public List<ModuleSlot> GetOrderedModuleSlots()
    {
        return ModuleSlots.OrderBy(ms => ms.SortOrder).ThenBy(ms => ms.Name).ToList();
    }

    public List<PluginModule> GetPluginModulesForSlot(ModuleSlot moduleSlot)
    {
        return PluginModules.Where(pm => pm.ModuleSlotId == moduleSlot.Id).ToList();
    }

    public override string ToString()
    {
        return Name;
    }
}

// A module slot type registered by the game (BasicModule, AdvancedModule, ...). Each crafting
// table exposes a subset of slots; one module can be installed per slot, all active at once.
public class ModuleSlot: IHasLocalizedName
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public int SortOrder { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<PluginModule> PluginModules { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];

    public override string ToString()
    {
        return Name;
    }
}

public class PluginModule: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }

    [ForeignKey("ModuleSlot")] public Guid? ModuleSlotId { get; set; }
    public decimal? MaterialTierBump { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public ModuleSlot? ModuleSlot { get; set; }
    public Server Server { get; set; }
    public List<TalentBonus> Bonuses { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; set; } = [];

    public string GetTooltip(LocalizationService localizationService)
    {
        var bonuses = Bonuses
            .Select(b => b.GetDescription(localizationService))
            .Where(d => d.Length > 0)
            .ToList();

        return localizationService.GetTranslation(this)
               + (bonuses.Count > 0 ? $" [{string.Join(", ", bonuses)}]" : "");
    }

    public override string ToString()
    {
        return Name;
    }
}

public class User
{
    [Key] public Guid Id { get; init; } = Guid.NewGuid();
    public string Pseudo { get; set; }
    public DateTimeOffset CreationDateTime { get; init; }
    public Guid SecretId { get; set; }
    public bool SuperAdmin { get; set; }
    public bool CanUploadMod { get; set; }
    public bool ShowHelp { get; set; }

    public List<UserServer> UserServers { get; set; } = [];
    public List<ModUploadHistory> ModUploadHistories { get; set; } = [];
}

public class UserServer
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string? Pseudo { get; set; }
    public string? EcoUserId { get; set; }
    public bool IsAdmin { get; set; }
    [ForeignKey("User")] public Guid UserId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public User User { get; init; }
    public Server Server { get; init; }
    public List<DataContext> DataContexts { get; init; } = [];

    public string GetPseudo()
    {
        return Pseudo ?? User.Pseudo;
    }
}

public class DataContext
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsShoppingList { get; set; }

    public UserServer UserServer { get; set; }
    public List<UserSkill> UserSkills { get; init; } = [];
    public List<UserTalent> UserTalents { get; init; } = [];
    public List<UserElement> UserElements { get; init; } = [];
    public List<UserPrice> UserPrices { get; init; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; init; } = [];
    public List<UserSetting> UserSettings { get; set; } = [];
    public List<UserRecipe> UserRecipes { get; set; } = [];
    public List<UserMargin> UserMargins { get; set; } = [];
    public List<UserAutomationInput> UserAutomationInputs { get; set; } = [];
    public List<UserAutomationTarget> UserAutomationTargets { get; set; } = [];

    public List<UserRecipe> GetRootShoppingListRecipes()
    {
        var userRecipeIds = UserRecipes.Select(ur => ur.Id).ToHashSet();

        return UserRecipes
            .Where(ur => ur.ParentUserRecipeId is null || !userRecipeIds.Contains(ur.ParentUserRecipeId.Value))
            .ToList();
    }
}

public class UserSetting
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }

    public MarginType MarginType { get; set; } = MarginType.MarkUp;
    public decimal CalorieCost { get; set; } = 0;
    public bool DisplayNonSkilledRecipes { get; set; } = false;
    public bool OnlyLevelAccessibleRecipes { get; set; } = false;
    public bool ApplyMarginBetweenSkills { get; set; } = true;

    public DataContext DataContext { get; set; }
}

// Limite d'entrée saisie dans le planificateur d'automatisation d'une shopping list : plafond de débit
// (par minute) d'une matière première donnée. Persistée par shopping list (DataContext) et par item.
// Une ligne n'existe que si une limite est effectivement saisie (sinon « pas de limite »).
public class UserAutomationInput
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }

    public decimal Cap { get; set; }

    public DataContext DataContext { get; set; }
    public ItemOrTag ItemOrTag { get; set; }
}

// Objectif de calcul saisi dans le planificateur d'automatisation d'une shopping list : débit cible
// (par minute) d'un produit final, ou mode « max » (production maximale sous les contraintes d'entrée).
// Persisté par shopping list (DataContext) et par item. Une ligne existe dès que l'utilisateur a touché
// la cible (débit modifié ou « max » activé) ; sinon le débit par défaut est recalculé à l'ouverture.
public class UserAutomationTarget
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }

    // Débit cible /min (sert aussi de base quand IsMax : conserve un éventuel débit saisi avant « max »).
    public decimal Rate { get; set; }
    public bool IsMax { get; set; }

    public DataContext DataContext { get; set; }
    public ItemOrTag ItemOrTag { get; set; }
}

public class UserMargin
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    public string Name { get; set; } = "";

    public decimal Margin { get; set; } = 0;
    public RoundingMode Rounding { get; set; } = RoundingMode.None;

    public DataContext DataContext { get; set; }
    public List<UserPrice> UserPrices { get; set; } = [];
}

public class UserCraftingTable
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
    [ForeignKey("FuelItem")] public Guid? FuelItemId { get; set; }

    public decimal AdditionalCraftMinuteFee { get; set; } = 0;
    public decimal TotalCraftMinuteFee { get; set; } = 0;

    public DataContext DataContext { get; set; }
    public CraftingTable CraftingTable { get; set; }
    public ItemOrTag? FuelItem { get; set; }

    // Installed modules: at most one per module slot, all applying their bonuses simultaneously.
    public List<PluginModule> PluginModules { get; set; } = [];

    public PluginModule? GetPluginModuleForSlot(ModuleSlot moduleSlot)
    {
        return PluginModules.FirstOrDefault(pm => pm.ModuleSlotId == moduleSlot.Id);
    }

    // Installs (or clears, when pluginModule is null) the module of one slot, enforcing the
    // at-most-one-module-per-slot invariant in a single place. Returns false when nothing changed.
    public bool SetPluginModuleForSlot(ModuleSlot moduleSlot, PluginModule? pluginModule)
    {
        var currentModule = GetPluginModuleForSlot(moduleSlot);

        if (currentModule?.Id == pluginModule?.Id)
        {
            return false;
        }

        if (currentModule is not null)
        {
            PluginModules.Remove(currentModule);
        }

        if (pluginModule is not null)
        {
            PluginModules.Add(pluginModule);
        }

        return true;
    }

    // Effective required room material tier of the table: base requirement + installed module bumps.
    public decimal? GetEffectiveRoomMaterialTier()
    {
        var bump = PluginModules.Sum(pm => pm.MaterialTierBump ?? 0m);

        if (CraftingTable.RoomMaterialTier is null) return bump > 0 ? bump : null;

        return CraftingTable.RoomMaterialTier.Value + bump;
    }
}

public class UserSkill
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public int Level { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }

    public Skill? Skill { get; set; }
    public DataContext DataContext { get; set; }
}

public class UserTalent
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Talent")] public Guid TalentId { get; set; }
    public int Level { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }

    public Talent Talent { get; set; }
    public DataContext DataContext { get; set; }
}

internal interface IHasPrice
{
    public decimal? Price { get; set; }
}

public class UserElement: IHasPrice
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Element")] public Guid ElementId { get; set; }

    public decimal? Price { get; set; }
    public bool IsMarginPrice { get; set; }

    public decimal Share { get; set; }
    public bool IsReintegrated { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    [ForeignKey("UserRecipe")] public Guid UserRecipeId { get; set; }

    public Element Element { get; set; }
    public DataContext DataContext { get; set; }
    public UserRecipe UserRecipe { get; set; }
    public List<UserPrice> UserPricesPrimaryOf { get; set; } = [];
}

public class UserPrice: IHasPrice
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }

    public decimal? Price { get; set; }
    public decimal? MarginPrice { get; set; }

    [ForeignKey("UserElement")] public Guid? PrimaryUserElementId { get; set; }
    [ForeignKey("UserPrice")] public Guid? PrimaryUserPriceId { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    public bool OverrideIsBought { get; set; }

    [ForeignKey("UserMargin")] public Guid? UserMarginId { get; set; }
    public UserMargin? UserMargin { get; set; }
    public ItemOrTag ItemOrTag { get; set; }
    public DataContext DataContext { get; set; }
    public UserElement? PrimaryUserElement { get; set; }
    public UserPrice? PrimaryUserPrice { get; set; }

    public decimal? GetMarginPriceOrPrice()
    {
        return MarginPrice ?? Price;
    }

    public void SetPrices(decimal? price, MarginType? marginType)
    {
        Price = price;

        if (Price is null || UserMargin is null || marginType is null)
        {
            MarginPrice = null;
            return;
        }

        switch (marginType)
        {
            case MarginType.MarkUp:
                MarginPrice = Price * (1 + UserMargin.Margin / 100);
                break;
            case MarginType.GrossMargin:
            {
                var divisionFactor = 1 - UserMargin.Margin / 100;

                if (divisionFactor > 0)
                {
                    MarginPrice = Price / divisionFactor;
                }

                break;
            }
        }
    }
}

public class UserRecipe
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Recipe")] public Guid RecipeId { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    public int RoundFactor { get; set; }
    public bool LockShare { get; set; } = false;

    // Craft time (minutes) set by the user when the recipe's craft time is driven by a world Layer (null = unset)
    public decimal? CraftMinutesOverride { get; set; }

    // For Shopping List only
    [ForeignKey("UserRecipe")] public Guid? ParentUserRecipeId { get; set; }

    public Recipe Recipe { get; set; }
    public DataContext DataContext { get; set; }

    public UserRecipe? ParentUserRecipe { get; set; }
    public List<UserRecipe> ChildrenUserRecipes { get; set; } = [];
    public List<UserElement> UserElements { get; set; } = [];
}

// Server Data
public class Server
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string? EcoServerId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsCalorieCostLocked { get; set; } = false;
    public decimal? LockedCalorieCost { get; set; }
    public decimal? CalorieCostMin { get; set; }
    public decimal? CalorieCostDefault { get; set; }
    public decimal? CalorieCostMax { get; set; }
    public bool IsMarginLocked { get; set; } = false;
    public decimal? LockedMargin { get; set; }
    public decimal? MarginMin { get; set; }
    public decimal? MarginDefault { get; set; }
    public decimal? MarginMax { get; set; }
    public DateTimeOffset CreationDateTime { get; set; }
    public DateTimeOffset? LastDataUploadTime { get; set; }
	public string JoinCode { get; set; }
	public Guid ApiKey { get; set; } = Guid.NewGuid();

	// Active le bouton « Planificateur » (mode automatisation de la chaîne de production) pour ce
	// serveur. Activable uniquement par un super-admin depuis la page d'administration.
	public bool IsAutomationPlannerEnabled { get; set; } = false;

    [NotMapped]
    public bool IsEmpty { get; set; }

	public List<UserServer> UserServers { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];
    public List<ModuleSlot> ModuleSlots { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<ItemOrTag> ItemOrTags { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<DynamicValue> DynamicValues { get; set; } = [];
    public List<ModUploadHistory> ModUploadHistories { get; set; } = [];
}

// History
public class ModUploadHistory
{
    [Key] public Guid Id { get; init; } = Guid.NewGuid();
    public string FileName { get; set; }
    public string FileHash { get; set; }
    public int IconsCount { get; set; }
    public DateTimeOffset UploadDateTime { get; init; }
    [ForeignKey("User")] public Guid UserId { get; set; }
    [ForeignKey("Server")] public Guid? ServerId { get; set; }

    public User User { get; init; }
    public Server? Server { get; init; }
}

// Utils
public class LocalizedField
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Server")] public Guid ServerId { get; set; } // Added so it's deleted in cascade if server is deleted
    public string en_US { get; set; } = "";
    public string fr { get; set; } = "";
    public string es { get; set; } = "";
    public string de { get; set; } = "";
    public string ko { get; set; } = "";
    public string pt_BR { get; set; } = "";
    public string zh_Hans { get; set; } = "";
    public string ru { get; set; } = "";
    public string it { get; set; } = "";
    public string pt_PT { get; set; } = "";
    public string hu { get; set; } = "";
    public string ja { get; set; } = "";
    public string nn { get; set; } = "";
    public string pl { get; set; } = "";
    public string nl { get; set; } = "";
    public string ro { get; set; } = "";
    public string da { get; set; } = "";
    public string cs { get; set; } = "";
    public string sv { get; set; } = "";
    public string uk { get; set; } = "";
    public string el { get; set; } = "";
    public string ar_sa { get; set; } = "";
    public string vi { get; set; } = "";
    public string tr { get; set; } = "";

    public Server Server { get; set; }
    public List<Recipe> Recipes { get; set; } = [];
    public List<ItemOrTag> ItemOrTags { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<Talent> Talents { get; set; } = [];
    public List<Talent> TalentDescriptions { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];
    public List<ModuleSlot> ModuleSlots { get; set; } = [];

    public static Dictionary<SupportedLanguage, LanguageCode> SupportedLanguageToCode =
        new()
        {
            [SupportedLanguage.English] = LanguageCode.en_US,
            [SupportedLanguage.French] = LanguageCode.fr,
            [SupportedLanguage.Spanish] = LanguageCode.es,
            [SupportedLanguage.German] = LanguageCode.de,
            [SupportedLanguage.Korean] = LanguageCode.ko,
            [SupportedLanguage.BrazilianPortuguese] = LanguageCode.pt_BR,
            [SupportedLanguage.SimplifiedChinese] = LanguageCode.zh_Hans,
            [SupportedLanguage.Russian] = LanguageCode.ru,
            [SupportedLanguage.Italian] = LanguageCode.it,
            [SupportedLanguage.Portuguese] = LanguageCode.pt_PT,
            [SupportedLanguage.Hungarian] = LanguageCode.hu,
            [SupportedLanguage.Japanese] = LanguageCode.ja,
            [SupportedLanguage.Norwegian] = LanguageCode.nn,
            [SupportedLanguage.Polish] = LanguageCode.pl,
            [SupportedLanguage.Dutch] = LanguageCode.nl,
            [SupportedLanguage.Romanian] = LanguageCode.ro,
            [SupportedLanguage.Danish] = LanguageCode.da,
            [SupportedLanguage.Czech] = LanguageCode.cs,
            [SupportedLanguage.Swedish] = LanguageCode.sv,
            [SupportedLanguage.Ukrainian] = LanguageCode.uk,
            [SupportedLanguage.Greek] = LanguageCode.el,
            [SupportedLanguage.Arabic] = LanguageCode.ar_sa,
            [SupportedLanguage.Vietnamese] = LanguageCode.vi,
            [SupportedLanguage.Turkish] = LanguageCode.tr
        };
}
