using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ecocraft.Models
{
    // User Model
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        // Global parameters
        public decimal CalorieCost { get; set; } // Cost per thousand calories
        public decimal ProfitMargin { get; set; } // Profit percentage

        public ICollection<UserSkill> UserSkills { get; set; }
        public ICollection<UserCraftingTable> UserCraftingTables { get; set; } // Nouvelle relation avec UserCraftingTables
        public ICollection<UserInputPrice> UserInputPrices { get; set; }

		
	}

    // Skill Model
    public class Skill
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<UserSkill> UserSkills { get; set; }
        public ICollection<Recipe> Recipes { get; set; }

        // Relation Many-to-Many avec CraftingTable via CraftingTableSkill
        public ICollection<CraftingTableSkill> CraftingTableSkills { get; set; }
    }

    // UserSkill Model
    public class UserSkill
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }

        [ForeignKey("Skill")]
        public int SkillId { get; set; }
        public Skill Skill { get; set; }

        public int Level { get; set; } // From 0 to 7
    }

    // CraftingTable Model
    public class CraftingTable
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<Recipe> Recipes { get; set; }
        public ICollection<CraftingTableUpgrade> CraftingTableUpgrades { get; set; }

        // Relation Many-to-Many avec Skill via CraftingTableSkill
        public ICollection<CraftingTableSkill> CraftingTableSkills { get; set; }
    }

    public class CraftingTableSkill
    {
        // ForeignKey vers CraftingTable
        [ForeignKey("CraftingTable")]
        public int CraftingTableId { get; set; }
        public CraftingTable CraftingTable { get; set; }

        // ForeignKey vers Skill
        [ForeignKey("Skill")]
        public int SkillId { get; set; }
        public Skill Skill { get; set; }
    }

    // Recipe Model
    public class Recipe
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        // Foreign key to CraftingTable
        [ForeignKey("CraftingTable")]
        public int CraftingTableId { get; set; }
        public CraftingTable CraftingTable { get; set; }

        // Calories required for crafting
        public int CaloriesRequired { get; set; }

        // Foreign key to Skill
        [ForeignKey("Skill")]
        public int SkillId { get; set; }
        public Skill Skill { get; set; }

        // Minimum skill level required
        public int MinimumSkillLevel { get; set; }

        // Collection of RecipeMaterials
        public ICollection<RecipeMaterial> RecipeMaterials { get; set; }

        // Collection of RecipeOutputs
        public ICollection<RecipeOutput> RecipeOutputs { get; set; }
    }

    // RecipeMaterial Model
    public class RecipeMaterial
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; }

        public string MaterialName { get; set; }
        public int Quantity { get; set; }
        public bool IsFixedQuantity { get; set; } // Whether the material amount is affected by upgrades
    }

    // RecipeOutput Model
    public class RecipeOutput
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; }

        public string OutputName { get; set; }
        public int Quantity { get; set; }
    }

    // Upgrade Model
    public class Upgrade
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        public string Description { get; set; }
        public float CostReduction { get; set; } // Reduction percentage for both resource and calorie cost

        public ICollection<CraftingTableUpgrade> CraftingTableUpgrades { get; set; } // For crafting tables using this upgrade
    }

    // CraftingTableUpgrade Model
    public class CraftingTableUpgrade
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("CraftingTable")]
        public int CraftingTableId { get; set; }
        public CraftingTable CraftingTable { get; set; }

        [ForeignKey("Upgrade")]
        public int UpgradeId { get; set; }
        public Upgrade Upgrade { get; set; }
    }

    // UserCraftingTable Model
    public class UserCraftingTable
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }

        [ForeignKey("CraftingTable")]
        public int CraftingTableId { get; set; }
        public CraftingTable CraftingTable { get; set; }

        [ForeignKey("Upgrade")]
        public int UpgradeId { get; set; }
        public Upgrade Upgrade { get; set; }
    }

    // UserInputPrice Model
    public class UserInputPrice
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }

        public string InputName { get; set; }
        public decimal Price { get; set; } // Price of the input material for the user
    }
}
