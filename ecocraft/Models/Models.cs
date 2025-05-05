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

public interface ISLinkedToModifier;

// Eco Data
public class Recipe: IHasLocalizedName
{
    [Key] public Guid Id { get; set; }
    public required string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public required string FamilyName { get; set; }
    [ForeignKey("DynamicValue")] public Guid CraftMinutesId { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public long SkillLevel { get; set; }
    public bool IsBlueprint { get; set; }
    public bool IsDefault { get; set; }
    [ForeignKey("DynamicValue")] public Guid LaborId { get; set; }
    [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

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
        return UserRecipes.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
    }

    public override string ToString()
    {
        return Name;
    }
}

public class Element
{
    [Key] public Guid Id { get; set; }
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

    public UserElement? GetCurrentUserElement(DataContext dataContext)
    {
        return UserElements.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
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
    [Key] public Guid Id { get; set; }
    public decimal BaseValue { get; set; }
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

    public decimal GetMultiplier(DataContext dataContext)
    {
        var multiplier = 1m;

        foreach (var modifier in Modifiers)
        {
            switch (modifier.DynamicType)
            {
                case "Module":
                    multiplier *= (Recipe ?? Element?.Recipe)?.CraftingTable.GetCurrentUserCraftingTable(dataContext)?.GetBestPluginModule(modifier.Skill)?.GetPercent(modifier.Skill) ?? 1m;
                    break;
                case "Talent":
                    multiplier *= modifier.Talent?.GetCurrentUserTalent(dataContext) is not null ? modifier.Talent.Value : 1m;
                    break;
                case "Skill":
                    multiplier *= modifier.Skill?.GetCurrentUserSkill(dataContext) is not null ? modifier.Skill.GetLevelLaborReducePercent(modifier.Skill.GetCurrentUserSkill(dataContext)!.Level) : 1m;
                    break;
                /*case "Layer":

                    break;*/
            }
        }

        return multiplier;
    }

    public decimal GetBaseValue()
    {
        return BaseValue;
    }

    public decimal GetRoundFactorBaseValue(DataContext dataContext)
    {
        var roundFactor = (Recipe ?? Element?.Recipe)!.GetCurrentUserRecipe(dataContext)!.RoundFactor;

        if (roundFactor == 0) return BaseValue;

        return BaseValue < 0
            ? Math.Floor(BaseValue * roundFactor) / roundFactor
            : Math.Ceiling(BaseValue * roundFactor) / roundFactor;
    }

    public decimal GetDynamicValue(DataContext dataContext)
    {
        return BaseValue * GetMultiplier(dataContext);
    }

    public decimal GetRoundFactorDynamicValue(DataContext dataContext)
    {
        var roundFactor = (Recipe ?? Element?.Recipe)!.GetCurrentUserRecipe(dataContext)!.RoundFactor;

        if (roundFactor == 0) return GetDynamicValue(dataContext);

        return GetDynamicValue(dataContext) < 0
            ? Math.Floor(GetDynamicValue(dataContext) * roundFactor) / roundFactor
            : Math.Ceiling(GetDynamicValue(dataContext) * roundFactor) / roundFactor;
    }

    public string GetMultiplierTooltip(DataContext dataContext, LocalizationService localizationService, string? baseValue = null)
    {
        baseValue ??= Math.Abs(Math.Round(GetBaseValue(), 0, MidpointRounding.AwayFromZero)).ToString();

        List<string> tooltip = [];
        decimal totalMultiplier = 1;

        foreach (var modifier in Modifiers)
        {
            decimal multiplier = 1m;

            switch (modifier.DynamicType)
            {
                case "Module":
                    var bestPluginModule = (Recipe ?? Element?.Recipe)?.CraftingTable.GetCurrentUserCraftingTable(dataContext)?.GetBestPluginModule(modifier.Skill);
                    multiplier *= bestPluginModule?.GetPercent(modifier.Skill) ?? 1m;

                    if (multiplier != 1m)
                    {
                        tooltip.Add(localizationService.GetTranslation(
                            "RecipeDialog.ModuleReductionTooltip",
                            localizationService.GetTranslation(bestPluginModule),
                            Math.Round(100 - multiplier * 100, 1, MidpointRounding.AwayFromZero).ToString("0.##")
                        ));
                    }
                    break;
                case "Talent":
                    multiplier = modifier.Talent?.GetCurrentUserTalent(dataContext) is not null ? modifier.Talent.Value : 1m;

                    if (multiplier != 1m)
                    {
                        tooltip.Add(localizationService.GetTranslation(
                            "RecipeDialog.TalentReductionTooltip",
                            localizationService.GetTranslation(modifier.Talent),
                            Math.Round(100 - multiplier * 100, 1, MidpointRounding.AwayFromZero).ToString("0.##")
                        ));
                    }
                    break;
                case "Skill":
                    multiplier = modifier.Skill?.GetCurrentUserSkill(dataContext) is not null ? modifier.Skill.GetLevelLaborReducePercent(modifier.Skill.GetCurrentUserSkill(dataContext)!.Level) : 1m;

                    if (multiplier != 1m)
                    {
                        tooltip.Add(localizationService.GetTranslation(
                            "RecipeDialog.SkillReductionTooltip",
                            localizationService.GetTranslation(modifier.Skill),
                            modifier.Skill!.GetCurrentUserSkill(dataContext)!.Level.ToString(),
                            Math.Round(100 - multiplier * 100, 1, MidpointRounding.AwayFromZero).ToString("0.##")
                        ));
                    }
                    break;
                /*case "Layer":

                    break;*/
            }

            totalMultiplier *= multiplier;
        }

        var beginning = localizationService.GetTranslation(
            "RecipeDialog.TotalReductionTooltip",
            Math.Round(100 - totalMultiplier * 100, 1, MidpointRounding.AwayFromZero).ToString("0.##")
        );

        return baseValue + " " + beginning + string.Join(", ", tooltip);
    }
}

public class Modifier
{
    [Key] public Guid Id { get; set; }
    public required string DynamicType { get; set; }
    [ForeignKey("DynamicValue")] public Guid DynamicValueId { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    [ForeignKey("Talent")] public Guid? TalentId { get; set; }

    public DynamicValue DynamicValue { get; set; }
    public Skill? Skill { get; set; }
    public Talent? Talent { get; set; }
}

public class ItemOrTag: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public required string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public bool IsTag { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<Element> Elements { get; set; } = [];
    public List<UserPrice> UserPrices { get; set; } = [];
    public List<ItemOrTag> AssociatedTags { get; set; } = [];
    public List<ItemOrTag> AssociatedItems { get; set; } = [];

    public UserPrice? GetCurrentUserPrice(DataContext dataContext)
    {
        return UserPrices.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
    }

    public override string ToString()
    {
        return Name;
    }
}

public class Skill: IHasLocalizedName, IHasIconName, ISLinkedToModifier
{
    [Key] public Guid Id { get; set; }
    public required string Name { get; set; }
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
    public List<PluginModule> PluginModules { get; set; } = [];

    public UserSkill? GetCurrentUserSkill(DataContext dataContext)
    {
        return UserSkills.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
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

public class Talent: IHasLocalizedName, ISLinkedToModifier, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public required string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public required string TalentGroupName { get; set; }
    public decimal Value { get; set; }
    public int Level { get; set; }
    [ForeignKey("Skill")] public Guid SkillId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Skill Skill { get; set; }
    public List<Modifier> Modifiers { get; set; } = [];
    public List<UserTalent> UserTalents { get; set; } = [];

    public UserTalent? GetCurrentUserTalent(DataContext dataContext)
    {
        return UserTalents.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
    }
}

public class CraftingTable: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public required string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<UserCraftingTable> UserCraftingTables { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];

    public UserCraftingTable? GetCurrentUserCraftingTable(DataContext dataContext)
    {
        return UserCraftingTables.FirstOrDefault(ur => ur.DataContextId == dataContext.Id);
    }

    public override string ToString()
    {
        return Name;
    }
}

public enum PluginType
{
    None = 0,
    Resource = 1,
    Speed = 2,
    ResourceAndSpeed = 3,
}

public class PluginModule: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public required string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }

    public PluginType PluginType { get; set; }
    public decimal Percent { get; set; }
    public decimal? SkillPercent { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Skill? Skill { get; set; }
    public Server Server { get; set; }
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; set; } = [];

    public decimal GetPercent(Skill? recipeSkill)
    {
        if (recipeSkill is not null && recipeSkill == Skill && SkillPercent is not null)
        {
            return (decimal)SkillPercent;
        }

        return Percent;
    }

    public string GetTooltip(LocalizationService localizationService)
    {
        return localizationService.GetTranslation(this)
               + $" [{((1 - Percent) * 100).ToString("0.##")}%]"
               + (Skill is not null
                   ? $" - {localizationService.GetTranslation(Skill)}: [{((1 - (decimal)SkillPercent!) * 100).ToString("0.##")}%]"
                   : "");
    }

    public override string ToString()
    {
        return Name;
    }
}

// User Data
public class User
{
    [Key] public Guid Id { get; init; }
    public string Pseudo { get; set; }
    public DateTime CreationDateTime { get; init; }
    public Guid SecretId { get; set; }
    public bool SuperAdmin { get; set; }
    public bool CanUploadMod { get; set; }
    public bool ShowHelp { get; set; }

    public List<UserServer> UserServers { get; set; } = [];
    public List<ModUploadHistory> ModUploadHistories { get; set; } = [];
}

public class UserServer
{
    [Key] public Guid Id { get; set; }
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
        return Pseudo is not null ? Pseudo : User.Pseudo;
    }
}

public class DataContext
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }

    public UserServer UserServer { get; set; }
    public List<UserSkill> UserSkills { get; init; } = [];
    public List<UserTalent> UserTalents { get; init; } = [];
    public List<UserElement> UserElements { get; init; } = [];
    public List<UserPrice> UserPrices { get; init; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; init; } = [];
    public List<UserSetting> UserSettings { get; init; } = [];
    public List<UserRecipe> UserRecipes { get; init; } = [];
    public List<UserMargin> UserMargins { get; init; } = [];
}

public class UserSetting
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }

    public MarginType MarginType { get; set; } = MarginType.MarkUp;
    public decimal CalorieCost { get; set; } = 0;
    public bool DisplayNonSkilledRecipes { get; set; } = false;
    public bool OnlyLevelAccessibleRecipes { get; set; } = false;
    public bool ApplyMarginBetweenSkills { get; set; } = true;

    public DataContext DataContext { get; set; }
}

public class UserMargin
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    public string Name { get; set; } = "";

    public decimal Margin { get; set; } = 0;

    public DataContext DataContext { get; set; }
    public List<UserPrice> UserPrices { get; set; } = [];
}

public class UserCraftingTable
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
    [ForeignKey("PluginModule")] public Guid? PluginModuleId { get; set; }

    public decimal CraftMinuteFee { get; set; } = 0;

    public DataContext DataContext { get; set; }
    public CraftingTable CraftingTable { get; set; }
    public PluginModule? PluginModule { get; set; }
    public List<PluginModule> SkilledPluginModules { get; set; } = [];

    public PluginModule? GetBestPluginModule(Skill? skill)
    {
        return SkilledPluginModules
            .Concat([PluginModule])
            .Where(pm => pm is not null)
            .MinBy(pm => pm!.GetPercent(skill));
    }
}

public class UserSkill
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public int Level { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }

    public Skill? Skill { get; set; }
    public DataContext DataContext { get; set; }
}

public class UserTalent
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("Talent")] public Guid TalentId { get; set; }
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
    [Key] public Guid Id { get; set; }
    [ForeignKey("Element")] public Guid ElementId { get; set; }

    public decimal? Price { get; set; }
    public bool IsMarginPrice { get; set; }

    public decimal Share { get; set; }
    public bool IsReintegrated { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }

    public Element Element { get; set; }
    public DataContext DataContext { get; set; }
}

public class UserPrice: IHasPrice
{
    [Key] public Guid Id { get; set; }
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
    [Key] public Guid Id { get; set; }
    [ForeignKey("Recipe")] public Guid RecipeId { get; set; }
    [ForeignKey("DataContext")] public Guid DataContextId { get; set; }
    public int RoundFactor { get; set; }

    public Recipe Recipe { get; set; }
    public DataContext DataContext { get; set; }
}

// Server Data
public class Server
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    public string? EcoServerId { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreationDateTime { get; set; }
	public string JoinCode { get; set; }

    [NotMapped]
    public bool IsEmpty { get; set; }

	public List<UserServer> UserServers { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<ItemOrTag> ItemOrTags { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<DynamicValue> DynamicValues { get; set; } = [];
    public List<ModUploadHistory> ModUploadHistories { get; set; } = [];
}

// History
public class ModUploadHistory
{
    [Key] public Guid Id { get; init; }
    public required string FileName { get; set; }
    public required string FileHash { get; set; }
    public required int IconsCount { get; set; }
    public required DateTime UploadDateTime { get; init; }
    [ForeignKey("User")] public Guid UserId { get; set; }
    [ForeignKey("Server")] public Guid? ServerId { get; set; }

    public User User { get; init; }
    public Server? Server { get; init; }
}

// Utils
public class LocalizedField
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; } // Added so it's deleted in cascade if server is deleted
    public string en_US { get; set; }
    public string fr { get; set; }
    public string es { get; set; }
    public string de { get; set; }
    public string ko { get; set; }
    public string pt_BR { get; set; }
    public string zh_Hans { get; set; }
    public string ru { get; set; }
    public string it { get; set; }
    public string pt_PT { get; set; }
    public string hu { get; set; }
    public string ja { get; set; }
    public string nn { get; set; }
    public string pl { get; set; }
    public string nl { get; set; }
    public string ro { get; set; }
    public string da { get; set; }
    public string cs { get; set; }
    public string sv { get; set; }
    public string uk { get; set; }
    public string el { get; set; }
    public string ar_sa { get; set; }
    public string vi { get; set; }
    public string tr { get; set; }

    public Server Server { get; set; }
    public List<Recipe> Recipes { get; set; } = [];
    public List<ItemOrTag> ItemOrTags { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<Talent> Talents { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];

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
