using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ecocraft.Extensions;

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

// Eco Data
public class Recipe: IHasLocalizedName
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public string FamilyName { get; set; }

    public decimal CraftMinutes { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public long SkillLevel { get; set; }
    public bool IsBlueprint { get; set; }
    public bool IsDefault { get; set; }

    public decimal Labor { get; set; }
    [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Skill? Skill { get; set; }
    public CraftingTable CraftingTable { get; set; }
    public Server Server { get; set; }
    public List<Element> Elements { get; set; } = [];
    public List<UserRecipe> UserRecipes { get; set; } = [];

    [NotMapped]
    public UserRecipe? CurrentUserRecipe { get; set; }

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

    public decimal Quantity { get; set; }
    public bool IsDynamic { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public bool LavishTalent { get; set; }
    public bool DefaultIsReintegrated { get; set; }

    public decimal DefaultShare { get; set; }

    public Recipe Recipe { get; set; }
    public ItemOrTag ItemOrTag { get; set; }
    public Skill? Skill { get; set; }
    public List<UserElement> UserElements { get; set; } = [];

    [NotMapped]
    public UserElement? CurrentUserElement { get; set; }

    public bool IsIngredient()
    {
        return Quantity < 0;
    }

    public bool IsProduct()
    {
        return Quantity > 0;
    }

    public override string ToString()
    {
        return ItemOrTag.Name;
    }
}

public class ItemOrTag: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public bool IsTag { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<Element> Elements { get; set; } = [];
    public List<UserPrice> UserPrices { get; set; } = [];
    public List<ItemOrTag> AssociatedTags { get; set; } = [];
    public List<ItemOrTag> AssociatedItems { get; set; } = [];

    [NotMapped]
    public UserPrice? CurrentUserPrice { get; set; }

    public override string ToString()
    {
        return Name;
    }
}

public class Skill: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    public string? Profession { get; set; }
    public decimal[] LaborReducePercent { get; set; }

    public decimal? LavishTalentValue { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<Recipe> Recipes { get; set; } = [];
    public List<UserSkill> UserSkills { get; set; } = [];

    [NotMapped]
    public UserSkill? CurrentUserSkill { get; set; }

    public override string ToString()
    {
        return Name;
    }
}

public class CraftingTable: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<UserCraftingTable> UserCraftingTables { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];

    [NotMapped]
    public UserCraftingTable? CurrentUserCraftingTable { get; set; }

    public override string ToString()
    {
        return Name;
    }
}

public class PluginModule: IHasLocalizedName, IHasIconName
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; }
    [ForeignKey("LocalizedField")] public Guid? LocalizedNameId { get; set; }

    public decimal Percent { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }

    public LocalizedField LocalizedName { get; set; }
    public Server Server { get; set; }
    public List<CraftingTable> CraftingTables { get; set; } = [];

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
    public bool ShowHelp { get; set; }

    public List<UserServer> UserServers { get; set; } = [];
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
    public List<UserSkill> UserSkills { get; init; } = [];
    public List<UserElement> UserElements { get; init; } = [];
    public List<UserPrice> UserPrices { get; init; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; init; } = [];
    public List<UserSetting> UserSettings { get; init; } = [];
    public List<UserRecipe> UserRecipes { get; init; } = [];
    public List<UserMargin> UserMargins { get; init; } = [];

    public string GetPseudo()
    {
        return Pseudo is not null ? this.Pseudo : this.User.Pseudo;
    }
}

public class UserSetting
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }

    public MarginType MarginType { get; set; } = MarginType.MarkUp;
    public decimal CalorieCost { get; set; } = 0;
    public bool DisplayNonSkilledRecipes { get; set; } = false;
    public bool OnlyLevelAccessibleRecipes { get; set; } = false;

    public UserServer UserServer { get; set; }
}

public class UserMargin
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
    public string Name { get; set; } = "";

    public decimal Margin { get; set; } = 0;

    public UserServer UserServer { get; set; }
    public List<UserPrice> UserPrices { get; set; } = [];
}

public class UserCraftingTable
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
    [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
    [ForeignKey("PluginModule")] public Guid? PluginModuleId { get; set; }

    public decimal CraftMinuteFee { get; set; } = 0;

    public UserServer UserServer { get; set; }
    public CraftingTable CraftingTable { get; set; }
    public PluginModule? PluginModule { get; set; }
}

public class UserSkill
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("Skill")] public Guid? SkillId { get; set; }
    public int Level { get; set; }
    public bool HasLavishTalent { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }

    public Skill? Skill { get; set; }
    public UserServer UserServer { get; set; }
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

    public decimal Share { get; set; }
    public bool IsReintegrated { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }

    public Element Element { get; set; }
    public UserServer UserServer { get; set; }

    public decimal GetRoundFactorQuantity(decimal factor = 1)
    {
        var roundFactor = Element.Recipe.CurrentUserRecipe!.RoundFactor;

        return roundFactor == 0
            ? Element.Quantity * factor
            : Element.Quantity < 0
                ? Math.Floor(Element.Quantity * roundFactor * factor) / roundFactor
                : Math.Ceiling(Element.Quantity * roundFactor * factor) / roundFactor;
    }
}

public class UserPrice: IHasPrice
{
    [Key] public Guid Id { get; set; }
    [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }

    public decimal? Price { get; set; }
    public decimal? MarginPrice { get; set; }

    [ForeignKey("UserElement")] public Guid? PrimaryUserElementId { get; set; }
    [ForeignKey("UserPrice")] public Guid? PrimaryUserPriceId { get; set; }
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
    public bool OverrideIsBought { get; set; }

    [ForeignKey("UserMargin")] public Guid? UserMarginId { get; set; }
    public UserMargin? UserMargin { get; set; }
    public ItemOrTag ItemOrTag { get; set; }
    public UserServer UserServer { get; set; }
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
    [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
    public int RoundFactor { get; set; }

    public Recipe Recipe { get; set; }
    public UserServer UserServer { get; set; }
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

	public List<UserServer> UserServers { get; set; } = [];
    public List<CraftingTable> CraftingTables { get; set; } = [];
    public List<PluginModule> PluginModules { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<ItemOrTag> ItemOrTags { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
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
