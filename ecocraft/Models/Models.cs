using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ecocraft.Models
{
    // Eco Data
    public class Recipe
    {
        [Key] public Guid Id { get; set; }
        public string Name { get; set; }
        public string FamilyName { get; set; }
        public float CraftMinutes { get; set; }
        [ForeignKey("Skill")] public Guid? SkillId { get; set; }
        public long SkillLevel { get; set; }
        public bool IsBlueprint { get; set; }
        public bool IsDefault { get; set; }
        public float Labor { get; set; }
        [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
        [ForeignKey("Server")] public Guid ServerId { get; set; }
        
        public Skill? Skill { get; set; }
        public CraftingTable CraftingTable { get; set; }
        public Server Server { get; set; }
        public List<Element> Elements { get; set; } = new List<Element>();
        public List<UserRecipe> UserRecipes { get; set; } = new List<UserRecipe>();

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
        public float Quantity { get; set; }
        public bool IsDynamic { get; set; }
        [ForeignKey("Skill")] public Guid? SkillId { get; set; }
        public bool LavishTalent { get; set; }
        
        public Recipe Recipe { get; set; }
        public ItemOrTag ItemOrTag { get; set; }
        public Skill? Skill { get; set; }
        public List<UserElement> UserElements { get; set; } = new List<UserElement>();

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

    public class ItemOrTag
    {
        [Key] public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsTag { get; set; }
        public float MinPrice { get; set; }
        public float MaxPrice { get; set; }
        [ForeignKey("Server")] public Guid ServerId { get; set; }
        
        public Server Server { get; set; }
        public List<Element> Elements { get; set; } = new List<Element>();
        public List<UserPrice> UserPrices { get; set; } = new List<UserPrice>();
        public List<ItemOrTag> AssociatedItemOrTags { get; set; } = new List<ItemOrTag>();

        public override string ToString()
        {
            return Name;
        }
    }

    public class Skill
    {
        [Key] public Guid Id { get; set; }
        public string Name { get; set; }
        [ForeignKey("Server")] public Guid ServerId { get; set; }
        
        public Server Server { get; set; }
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
        public List<UserSkill> UserSkills { get; set; } = new List<UserSkill>();

        public override string ToString()
        {
            return Name;
        }
    }

    public class CraftingTable
    {
        [Key] public Guid Id { get; set; }
        public string Name { get; set; }
        [ForeignKey("Server")] public Guid ServerId { get; set; }
        
        public Server Server { get; set; }
        public List<UserCraftingTable> UserCraftingTables { get; set; } = new List<UserCraftingTable>();
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
        public List<PluginModule> PluginModules { get; set; } = new List<PluginModule>();

        public override string ToString()
        {
            return Name;
        }
    }

    public class PluginModule
    {
        [Key] public Guid Id { get; set; }
        public string Name { get; set; }
        public float Percent { get; set; }
        [ForeignKey("Server")] public Guid ServerId { get; set; }
        
        public Server Server { get; set; }
        public List<CraftingTable> CraftingTables { get; set; } = new List<CraftingTable>();

        public override string ToString()
        {
            return Name;
        }
    }

    // User Data
    public class User
    {
        [Key] public Guid Id { get; set; }
        public string Pseudo { get; set; }
        public Guid SecretId { get; set; }

        public List<UserServer> UserServers { get; set; } = new List<UserServer>();
    }
    
    public class UserServer
    {
        [Key] public Guid Id { get; set; }
        public string Pseudo { get; set; }
        public bool IsAdmin { get; set; }         
        [ForeignKey("User")] public Guid UserId { get; set; }
        [ForeignKey("Server")] public Guid ServerId { get; set; }
        
        public User User { get; set; }
        public Server Server { get; set; }
        public List<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
        public List<UserElement> UserElements { get; set; } = new List<UserElement>();
        public List<UserPrice> UserPrices { get; set; } = new List<UserPrice>();
        public List<UserCraftingTable> UserCraftingTables { get; set; } = new List<UserCraftingTable>();
        public List<UserSetting> UserSettings { get; set; } = new List<UserSetting>();
        public List<UserRecipe> UserRecipes { get; set; } = new List<UserRecipe>();
    }

    public class UserSetting
    {
        [Key] public Guid Id { get; set; }
        [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
        public float CalorieCost { get; set; } = 0;
        public float Margin { get; set; } = 0;
        public float TimeFee { get; set; } = 0;

        public UserServer UserServer { get; set; }
    }

    public class UserCraftingTable
    {
        [Key] public Guid Id { get; set; }
        [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
        [ForeignKey("CraftingTable")] public Guid CraftingTableId { get; set; }
        [ForeignKey("PluginModule")] public Guid PluginModuleId { get; set; }
        
        public UserServer UserServer { get; set; }
        public CraftingTable CraftingTable { get; set; }
        public PluginModule? PluginModule { get; set; }
    }

    public class UserSkill
    {
        [Key] public Guid Id { get; set; }
        [ForeignKey("Skill")] public Guid SkillId { get; set; }		
        public int Level { get; set; }
        public bool HasLavishTalent { get; set; }
        [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
        
        public Skill Skill { get; set; }
        public UserServer UserServer { get; set; }
    }

    public class UserElement
    {
        [Key] public Guid Id { get; set; }
        [ForeignKey("Element")] public Guid ElementId { get; set; }
        public float Share { get; set; }
        [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
        
        public Element Element { get; set; }
        public UserServer UserServer { get; set; }
    }

    public class UserPrice
    {
        [Key] public Guid Id { get; set; }
        [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }		
        public float? Price { get; set; }         
        [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
        
        public ItemOrTag ItemOrTag { get; set; }
        public UserServer UserServer { get; set; }
    }

    public class UserRecipe
    {
        [Key] public Guid Id { get; set; }
        [ForeignKey("Recipe")] public Guid RecipeId { get; set; }		
        [ForeignKey("UserServer")] public Guid UserServerId { get; set; }
        
        public Recipe Recipe { get; set; }
        public UserServer UserServer { get; set; }
    }
    
    // Server Data
    public class Server
    {
        [Key] public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsDefault { get; set; }

        public List<UserServer> UserServers { get; set; } = new List<UserServer>();
        public List<CraftingTable> CraftingTables { get; set; } = new List<CraftingTable>();
        public List<PluginModule> PluginModules { get; set; } = new List<PluginModule>();
        public List<Skill> Skills { get; set; } = new List<Skill>();
        public List<ItemOrTag> ItemOrTags { get; set; } = new List<ItemOrTag>();
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
    }
}