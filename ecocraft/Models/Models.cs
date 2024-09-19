using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ecocraft.Models
{
	// User Model
	public class User
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Pseudo { get; set; }
		public Guid SecretId { get; set; } // Guid for secure identification

		// Navigation Properties
		public ICollection<UserSkill> UserSkills { get; set; }
		public ICollection<UserServer> UserServers { get; set; }
		public ICollection<UserProduct> UserProducts { get; set; }
		public ICollection<UserPrice> UserPrices { get; set; }
		public ICollection<UserCraftingTable> UserCraftingTables { get; set; }
		public ICollection<UserSetting> UserSettings { get; set; }
	}

	public class UserSetting
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }
		public float CalorieCost { get; set; }
		public float Margin { get; set; }
		public float TimeFee { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	public class UserServer
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }

		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
		
		
	}

	public class Server
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Name { get; set; }
		public string SecretId { get; set; }

		// Navigation Properties

		// UserServer
		public ICollection<UserServer> UserServers { get; set; } = new List<UserServer>();

		// CraftingTable
		public ICollection<CraftingTable> CraftingTables { get; set; } = new List<CraftingTable>();

		// PluginModule
		public ICollection<PluginModule> PluginModules { get; set; } = new List<PluginModule>();

		// Skill
		public ICollection<Skill> Skills { get; set; } = new List<Skill>();

		// UserPrice
		public ICollection<UserPrice> UserPrices { get; set; } = new List<UserPrice>();

		// ItemOrTag
		public ICollection<ItemOrTag> ItemOrTags { get; set; } = new List<ItemOrTag>();

		// ItemTagAssoc
		public ICollection<ItemTagAssoc> ItemTagAssocs { get; set; } = new List<ItemTagAssoc>();

		// Recipe
		public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

		// Ingredient
		public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

		// Product
		public ICollection<Product> Products { get; set; } = new List<Product>();

		// UserSetting
		public ICollection<UserSetting> UserSettings { get; set; } = new List<UserSetting>();

		// CraftingTablePluginModule
		public ICollection<CraftingTablePluginModule> CraftingTablePluginModules { get; set; } = new List<CraftingTablePluginModule>();
	}

	public class UserCraftingTable
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }

		[ForeignKey("CraftingTable")]
		public Guid CraftingTableId { get; set; } // Clé étrangère vers CraftingTable
		public CraftingTable CraftingTable { get; set; }

		[ForeignKey("PluginModule")]
		public Guid PluginModuleId { get; set; } // Clé étrangère vers CraftingTable
		public PluginModule PluginModule { get; set; }

		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	public class CraftingTable
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Name { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<UserCraftingTable> UserCraftingTables { get; set; }
		public ICollection<Recipe> Recipes { get; set; }
		public ICollection<CraftingTablePluginModule> CraftingTablePluginModules { get; set; }
	}

	public class CraftingTablePluginModule
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("CraftingTable")]
		public Guid CraftingTableId { get; set; } // Clé étrangère vers CraftingTable

		[ForeignKey("PluginModule")]
		public Guid PluginModuleId { get; set; } // Clé étrangère vers PluginModule

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public CraftingTable CraftingTable { get; set; }
		public PluginModule PluginModule { get; set; }
		public Server Server { get; set; }
	}

	public class Recipe
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Name { get; set; }
		public string FamilyName { get; set; }
		public float CraftMinutes { get; set; }

		[ForeignKey("Skill")]
		public Guid SkillId { get; set; } // Clé étrangère vers Skill
		public Skill Skill { get; set; }

		public int RequiredSkillLevel { get; set; }
		public bool IsBlueprint { get; set; }
		public bool IsDefault { get; set; }
		public float Labor { get; set; }

		[ForeignKey("CraftingTable")]
		public Guid CraftingTableId { get; set; } // Clé étrangère vers Skill
		public CraftingTable CraftingTable { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<Product> Products { get; set; }
		public ICollection<Ingredient> Ingredients { get; set; }
	}

	public class PluginModule
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Name { get; set; }
		public float Percent { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<CraftingTablePluginModule> CraftingTablePluginModules { get; set; }
		public ICollection<UserCraftingTable> UserCraftingTables { get; set; }
	}

	public class Skill
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Name { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<Recipe> Recipes { get; set; }
		public ICollection<UserSkill> UserSkills { get; set; }
	}

	public class UserSkill
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }

		[ForeignKey("Skill")]
		public Guid SkillId { get; set; } // Clé étrangère vers Skill		
		public Skill Skill { get; set; }

		public int Level { get; set; }
		public bool HasLavishTalent { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	public class UserProduct
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }

		[ForeignKey("Product")]
		public Guid ProductId { get; set; } // Clé étrangère vers Product
		public Product Product { get; set; }
		public float Share { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	public class Product
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("Recipe")]
		public Guid RecipeId { get; set; } // Clé étrangère vers Recipe
		public Recipe Recipe { get; set; }

		[ForeignKey("ItemOrTag")]
		public Guid ItemOrTagId { get; set; } // Clé étrangère vers ItemOrTag		
		public ItemOrTag ItemOrTag { get; set; }

		public bool LavishTalent { get; set; }
		public float Quantity { get; set; }
		public bool IsStatic { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<UserProduct> UserProducts { get; set; }
	}

	public class Ingredient
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("Recipe")]
		public Guid RecipeId { get; set; } // Clé étrangère vers Recipe
		public Recipe Recipe { get; set; }

		[ForeignKey("ItemOrTag")]
		public Guid ItemOrTagId { get; set; } // Clé étrangère vers ItemOrTag		
		public ItemOrTag ItemOrTag { get; set; }

		public float Quantity { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	

	public class UserPrice
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }

		[ForeignKey("ItemOrTag")]
		public Guid ItemOrTagId { get; set; } // Clé étrangère vers ItemOrTag		
		public ItemOrTag ItemOrTag { get; set; }

		public float Price { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	public class ItemOrTag
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire
		public string Name { get; set; }
		public float MinPrice { get; set; }
		public float MaxPrice { get; set; } // Bouger dans une table d'association

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<Product> Products { get; set; }
		public ICollection<Ingredient> Ingredients { get; set; }
		public ICollection<UserPrice> UserPrices { get; set; }
		public ICollection<Item> Items { get; set; }
		public ICollection<Tag> Tags { get; set; }
	}

	public class Item : ItemOrTag
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<ItemTagAssoc> ItemTagAssocs { get; set; }
	}

	public class Tag : ItemOrTag
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<ItemTagAssoc> ItemTagAssocs { get; set; }
	}

	public class ItemTagAssoc
	{
		[Key]
		public Guid Id { get; set; } // Clé primaire

		[ForeignKey("Item")]
		public Guid ItemId { get; set; } // Clé étrangère vers ItemOrTag
		public Item Item { get; set; }

		[ForeignKey("Tag")]
		public Guid TagId { get; set; } // Clé étrangère vers ItemOrTag		
		public Tag Tag { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}
}
