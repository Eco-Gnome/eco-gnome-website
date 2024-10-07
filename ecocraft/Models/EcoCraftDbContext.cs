using Microsoft.EntityFrameworkCore;

namespace ecocraft.Models;

public class EcoCraftDbContext : DbContext
{
	public EcoCraftDbContext(DbContextOptions<EcoCraftDbContext> options)
		: base(options)
	{
	}

	public DbSet<Recipe> Recipes { get; set; }
	public DbSet<Element> Elements { get; set; }
	public DbSet<ItemOrTag> ItemOrTags { get; set; }
	public DbSet<Skill> Skills { get; set; }
	public DbSet<CraftingTable> CraftingTables { get; set; }
	public DbSet<PluginModule> PluginModules { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<User> UserServers { get; set; }
	public DbSet<UserSetting> UserSettings { get; set; }
	public DbSet<UserCraftingTable> UserCraftingTables { get; set; }
	public DbSet<UserSkill> UserSkills { get; set; }
	public DbSet<UserElement> UserElements { get; set; }
	public DbSet<UserPrice> UserPrices { get; set; }
	public DbSet<UserRecipe> UserRecipes { get; set; }
	public DbSet<Server> Servers { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		
		// * Eco Data
		// Recipe
		modelBuilder.Entity<Recipe>()
			.ToTable("Recipe");
		
		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.Skill)
			.WithMany(s => s.Recipes)
			.HasForeignKey(r => r.SkillId);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.CraftingTable)
			.WithMany(ct => ct.Recipes)
			.HasForeignKey(r => r.CraftingTableId);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.Server)
			.WithMany(s => s.Recipes)
			.HasForeignKey(r => r.ServerId);
		
		// Element
		modelBuilder.Entity<Element>()
			.ToTable("Element");
		
		modelBuilder.Entity<Element>()
			.HasOne(e => e.Recipe)
			.WithMany(r => r.Elements)
			.HasForeignKey(e => e.RecipeId);

		modelBuilder.Entity<Element>()
			.HasOne(e => e.ItemOrTag)
			.WithMany(i => i.Elements)
			.HasForeignKey(e => e.ItemOrTagId);

		modelBuilder.Entity<Element>()
			.HasOne(p => p.Skill)
			.WithMany()
			.HasForeignKey(p => p.SkillId)
			.IsRequired(false);
		
		// ItemOrTag
		modelBuilder.Entity<ItemOrTag>()
			.ToTable("ItemOrTag");
		
		modelBuilder.Entity<ItemOrTag>()
			.HasOne(i => i.Server)
			.WithMany(s => s.ItemOrTags)
			.HasForeignKey(i => i.ServerId);
		
		modelBuilder.Entity<ItemOrTag>()
			.HasMany(iot => iot.AssociatedItemOrTags)
			.WithMany()
			.UsingEntity(
				"ItemTagAssoc",
				r => r.HasOne(typeof(ItemOrTag)).WithMany().HasForeignKey("ItemId")
					.HasPrincipalKey(nameof(ItemOrTag.Id)),
				l => l.HasOne(typeof(ItemOrTag)).WithMany().HasForeignKey("TagId")
					.HasPrincipalKey(nameof(ItemOrTag.Id)),
				j => j.HasKey("ItemId", "TagId"));
		
		// Skill
		modelBuilder.Entity<Skill>()
			.ToTable("Skill");
		
		modelBuilder.Entity<Skill>()
			.HasOne(s => s.Server)
			.WithMany(s => s.Skills)
			.HasForeignKey(s => s.ServerId);
		
		// CraftingTable
		modelBuilder.Entity<CraftingTable>()
			.ToTable("CraftingTable");
		
		modelBuilder.Entity<CraftingTable>()
			.HasOne(s => s.Server)
			.WithMany(s => s.CraftingTables)
			.HasForeignKey(s => s.ServerId);
		
		modelBuilder.Entity<CraftingTable>()
			.HasMany(ct => ct.PluginModules)
			.WithMany(pm => pm.CraftingTables)
			.UsingEntity(
				"CraftingTablePluginModule",
				r => r.HasOne(typeof(PluginModule)).WithMany().HasForeignKey("PluginModuleId")
					.HasPrincipalKey(nameof(PluginModule.Id)),
				l => l.HasOne(typeof(CraftingTable)).WithMany().HasForeignKey("CraftingTableId")
					.HasPrincipalKey(nameof(CraftingTable.Id)),
				j => j.HasKey("CraftingTableId", "PluginModuleId"));

		// PluginModule
		modelBuilder.Entity<PluginModule>()
			.ToTable("PluginModule");
		
		modelBuilder.Entity<PluginModule>()
			.HasOne(s => s.Server)
			.WithMany(s => s.PluginModules)
			.HasForeignKey(s => s.ServerId);
		
		// * User Data
		// User
		modelBuilder.Entity<User>()
			.ToTable("User");
		
		// UserSetting
		modelBuilder.Entity<UserSetting>()
			.ToTable("UserSetting");
		
		modelBuilder.Entity<UserSetting>()
			.HasOne(us => us.UserServer)
			.WithMany(us => us.UserSettings)
			.HasForeignKey(us => us.UserServerId);
		
		// UserCraftingTable
		modelBuilder.Entity<UserCraftingTable>()
			.ToTable("UserCraftingTable");

		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.UserServer)
			.WithMany(us => us.UserCraftingTables)
			.HasForeignKey(uct => uct.UserServerId);
		
		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.CraftingTable)
			.WithMany(u => u.UserCraftingTables)
			.HasForeignKey(uct => uct.CraftingTableId);
		
		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(s => s.PluginModule)
			.WithMany()
			.HasForeignKey(s => s.PluginModuleId)
			.IsRequired(false);
		
		// UserSkill
		modelBuilder.Entity<UserSkill>()
			.ToTable("UserSkill");

		modelBuilder.Entity<UserSkill>()
			.HasOne(us => us.Skill)
			.WithMany(s => s.UserSkills)
			.HasForeignKey(us => us.SkillId);

		modelBuilder.Entity<UserSkill>()
			.HasOne(us => us.UserServer)
			.WithMany(use => use.UserSkills)
			.HasForeignKey(us => us.UserServerId);
		
		// UserElement
		modelBuilder.Entity<UserElement>()
			.ToTable("UserElement");

		modelBuilder.Entity<UserElement>()
			.HasOne(ue => ue.Element)
			.WithMany(e => e.UserElements)
			.HasForeignKey(ue => ue.ElementId);
		
		modelBuilder.Entity<UserElement>()
			.HasOne(ue => ue.UserServer)
			.WithMany(us => us.UserElements)
			.HasForeignKey(ue => ue.UserServerId);
		
		// UserPrice
		modelBuilder.Entity<UserPrice>()
			.ToTable("UserPrice");

		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.ItemOrTag)
			.WithMany(iot => iot.UserPrices)
			.HasForeignKey(up => up.ItemOrTagId);
		
		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.UserServer)
			.WithMany(us => us.UserPrices)
			.HasForeignKey(up => up.UserServerId);
		
		// UserRecipe
		modelBuilder.Entity<UserRecipe>()
			.ToTable("UserRecipe");

		modelBuilder.Entity<UserRecipe>()
			.HasOne(up => up.Recipe)
			.WithMany(r => r.UserRecipes)
			.HasForeignKey(up => up.RecipeId);
		
		modelBuilder.Entity<UserRecipe>()
			.HasOne(ur => ur.UserServer)
			.WithMany(us => us.UserRecipes)
			.HasForeignKey(ur => ur.UserServerId);
		
		// UserServer
		modelBuilder.Entity<UserServer>()
			.ToTable("UserServer");

		modelBuilder.Entity<UserServer>()
			.HasOne(us => us.User)
			.WithMany(u => u.UserServers)
			.HasForeignKey(us => us.UserId);
			
		modelBuilder.Entity<UserServer>()
			.HasOne(us => us.Server)
			.WithMany(s => s.UserServers)
			.HasForeignKey(us => us.ServerId)
			.OnDelete(DeleteBehavior.Cascade);
		
		// * Server Data
		// Server
		modelBuilder.Entity<Server>()
			.ToTable("Server");
	}
}