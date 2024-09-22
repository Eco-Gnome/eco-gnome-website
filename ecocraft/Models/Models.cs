using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ecocraft.Models
{
	// User Model
	public class User
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Pseudo { get; set; }
		public Guid SecretId { get; set; } // Guid for secure identification

		// Navigation Properties
		public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
		public ICollection<UserServer> UserServers { get; set; } = new List<UserServer>();
		public ICollection<UserProduct> UserProducts { get; set; } = new List<UserProduct>();
		public ICollection<UserPrice> UserPrices { get; set; } = new List<UserPrice>();	
		public ICollection<UserCraftingTable> UserCraftingTables { get; set; } = new List<UserCraftingTable>();
		public ICollection<UserSetting> UserSettings { get; set; } = new List<UserSetting>();
	}

	public class UserSetting
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

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
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

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
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Name { get; set; } = string.Empty;
		public string SecretId { get; set; } = string.Empty;

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
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

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
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Name { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<UserCraftingTable> UserCraftingTables { get; set; } = new List<UserCraftingTable>();
		public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
		public ICollection<CraftingTablePluginModule> CraftingTablePluginModules { get; set; } = new List<CraftingTablePluginModule>();
	}

	public class CraftingTablePluginModule
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

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
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Name { get; set; }
		public string FamilyName { get; set; }
		public double CraftMinutes { get; set; }

		[ForeignKey("Skill")]
		public Guid? SkillId { get; set; } // Clé étrangère vers Skill
		public Skill? Skill { get; set; }

		public long RequiredSkillLevel { get; set; }
		public bool IsBlueprint { get; set; }
		public bool IsDefault { get; set; }
		public double Labor { get; set; }

		[ForeignKey("CraftingTable")]
		public Guid CraftingTableId { get; set; } // Clé étrangère vers Skill
		public CraftingTable CraftingTable { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<Product> Products { get; set; } = new List<Product>();
		public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
	}

	public class PluginModule
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Name { get; set; }
		public double Percent { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<CraftingTablePluginModule> CraftingTablePluginModules { get; set; } = new List<CraftingTablePluginModule>();
		public ICollection<UserCraftingTable> UserCraftingTables { get; set; } = new List<UserCraftingTable>();
	}

	public class Skill
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Name { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
		public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
	}

	public class UserSkill
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

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
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

		[ForeignKey("User")]
		public Guid UserId { get; set; } // Clé étrangère vers User
		public User User { get; set; }

		[ForeignKey("Product")]
		public Guid ProductId { get; set; } // Clé étrangère vers Product
		public Product Product { get; set; }
		public double Share { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	public class Product
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

		[ForeignKey("Recipe")]
		public Guid RecipeId { get; set; } // Clé étrangère vers Recipe
		public Recipe Recipe { get; set; }

		[ForeignKey("Item")]
		public Guid ItemId { get; set; } // Clé étrangère vers Item		
		public Item Item { get; set; }

		public bool LavishTalent { get; set; }
		public float Quantity { get; set; }
		public bool IsDynamic { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<UserProduct> UserProducts { get; set; } = new List<UserProduct>();
	}

	public class Ingredient
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

		[ForeignKey("Recipe")]
		public Guid RecipeId { get; set; } // Clé étrangère vers Recipe
		public Recipe Recipe { get; set; }

		[ForeignKey("ItemOrTag")]
		public Guid ItemOrTagId { get; set; } // Clé étrangère vers ItemOrTag		
		public ItemOrTag ItemOrTag { get; set; }

		public float Quantity { get; set; }
		public bool IsDynamic { get; set; }

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }
	}

	

	public class UserPrice
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire

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

	public abstract class ItemOrTag
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid(); // Clé primaire
		public string Name { get; set; }
		public float MinPrice { get; set; }
		public float MaxPrice { get; set; } // Bouger dans une table d'association

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<Product> Products { get; set; } = new List<Product>();
		public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
		public ICollection<UserPrice> UserPrices { get; set; } = new List<UserPrice>();
		public ICollection<Item> Items { get; set; } = new List<Item>();
		public ICollection<Tag> Tags { get; set; } = new List<Tag>();
	}

	public class Item : ItemOrTag
	{
		//[Key]
		//public Guid Id { get; set; } // Clé primaire

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<ItemTagAssoc> ItemTagAssocs { get; set; } = new List<ItemTagAssoc>();
	}

	public class Tag : ItemOrTag
	{
		//[Key]
		//public Guid Id { get; set; } // Clé primaire

		// Reference to Server
		[ForeignKey("Server")]
		public Guid ServerId { get; set; } // Clé étrangère vers Server
		public Server Server { get; set; }

		// Navigation Properties
		public ICollection<ItemTagAssoc> ItemTagAssocs { get; set; } = new List<ItemTagAssoc>();
	}

	public class ItemTagAssoc
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();// Clé primaire

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
