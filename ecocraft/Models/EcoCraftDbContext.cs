using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

public class EcoCraftDbContext : DbContext
{
	public DbSet<User> Users { get; set; }
	public DbSet<UserSetting> UserSettings { get; set; }
	public DbSet<UserServer> UserServers { get; set; }
	public DbSet<Server> Servers { get; set; }
	public DbSet<UserCraftingTable> UserCraftingTables { get; set; }
	public DbSet<CraftingTable> CraftingTables { get; set; }
	public DbSet<CraftingTablePluginModule> CraftingTablePluginModules { get; set; }
	public DbSet<Recipe> Recipes { get; set; }
	public DbSet<PluginModule> PluginModules { get; set; }
	public DbSet<Skill> Skills { get; set; }
	public DbSet<UserSkill> UserSkills { get; set; }
	public DbSet<UserProduct> UserProducts { get; set; }
	public DbSet<Product> Products { get; set; }
	public DbSet<Ingredient> Ingredients { get; set; }
	public DbSet<ItemOrTag> ItemOrTags { get; set; }
	public DbSet<UserPrice> UserPrices { get; set; }
	public DbSet<ItemTagAssoc> ItemTagAssocs { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// User and related entities
		modelBuilder.Entity<User>()
			.HasMany(u => u.UserSkills)
			.WithOne(us => us.User)
			.HasForeignKey(us => us.UserId);

		modelBuilder.Entity<User>()
			.HasMany(u => u.UserServers)
			.WithOne(us => us.User)
			.HasForeignKey(us => us.UserId);

		modelBuilder.Entity<User>()
			.HasMany(u => u.UserProducts)
			.WithOne(up => up.User)
			.HasForeignKey(up => up.UserId);

		modelBuilder.Entity<User>()
			.HasMany(u => u.UserPrices)
			.WithOne(up => up.User)
			.HasForeignKey(up => up.UserId);

		modelBuilder.Entity<User>()
			.HasMany(u => u.UserCraftingTables)
			.WithOne(uct => uct.User)
			.HasForeignKey(uct => uct.UserId);

		modelBuilder.Entity<User>()
			.HasMany(u => u.UserSettings)
			.WithOne(us => us.User)
			.HasForeignKey(us => us.UserId);

		// UserSetting
		modelBuilder.Entity<UserSetting>()
			.HasOne(us => us.Server)
			.WithMany(s => s.UserSettings)
			.HasForeignKey(us => us.ServerId);

		// UserServer
		modelBuilder.Entity<UserServer>()
			.HasOne(us => us.Server)
			.WithMany(s => s.UserServers)
			.HasForeignKey(us => us.ServerId);

		// Server and related entities
		modelBuilder.Entity<Server>()
			.HasMany(s => s.UserServers)
			.WithOne(us => us.Server)
			.HasForeignKey(us => us.ServerId);

		modelBuilder.Entity<Server>()
			.HasMany(s => s.CraftingTables)
			.WithOne(ct => ct.Server)
			.HasForeignKey(ct => ct.ServerId);

		modelBuilder.Entity<Server>()
			.HasMany(s => s.PluginModules)
			.WithOne(pm => pm.Server)
			.HasForeignKey(pm => pm.ServerId);

		modelBuilder.Entity<Server>()
			.HasMany(s => s.Skills)
			.WithOne(skill => skill.Server)
			.HasForeignKey(skill => skill.ServerId);

		modelBuilder.Entity<Server>()
			.HasMany(s => s.UserPrices)
			.WithOne(up => up.Server)
			.HasForeignKey(up => up.ServerId);

		modelBuilder.Entity<Server>()
			.HasMany(s => s.ItemOrTags)
			.WithOne(iot => iot.Server)
			.HasForeignKey(iot => iot.ServerId);

		modelBuilder.Entity<Server>()
			.HasMany(s => s.ItemTagAssocs)
			.WithOne(ita => ita.Server)
			.HasForeignKey(ita => ita.ServerId);

		// CraftingTable
		modelBuilder.Entity<CraftingTable>()
			.HasMany(ct => ct.UserCraftingTables)
			.WithOne(uct => uct.CraftingTable)
			.HasForeignKey(uct => uct.CraftingTableId);

		modelBuilder.Entity<CraftingTable>()
			.HasMany(ct => ct.Recipes)
			.WithOne(r => r.CraftingTable)
			.HasForeignKey(r => r.CraftingTableId);

		modelBuilder.Entity<CraftingTable>()
			.HasMany(ct => ct.CraftingTablePluginModules)
			.WithOne(ctpm => ctpm.CraftingTable)
			.HasForeignKey(ctpm => ctpm.CraftingTableId);

		// CraftingTablePluginModule
		modelBuilder.Entity<CraftingTablePluginModule>()
			.HasOne(ctpm => ctpm.CraftingTable)
			.WithMany(ct => ct.CraftingTablePluginModules)
			.HasForeignKey(ctpm => ctpm.CraftingTableId);

		modelBuilder.Entity<CraftingTablePluginModule>()
			.HasOne(ctpm => ctpm.PluginModule)
			.WithMany(pm => pm.CraftingTablePluginModules)
			.HasForeignKey(ctpm => ctpm.PluginModuleId);

		modelBuilder.Entity<CraftingTablePluginModule>()
			.HasOne(ctpm => ctpm.Server)
			.WithMany(s => s.CraftingTablePluginModules)
			.HasForeignKey(ctpm => ctpm.ServerId);

		// Recipe
		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.Skill)
			.WithMany(s => s.Recipes)
			.HasForeignKey(r => r.SkillId);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.CraftingTable)
			.WithMany(ct => ct.Recipes)
			.HasForeignKey(r => r.CraftingTableId);

		modelBuilder.Entity<Recipe>()
			.HasMany(r => r.Products)
			.WithOne(p => p.Recipe)
			.HasForeignKey(p => p.RecipeId);

		modelBuilder.Entity<Recipe>()
			.HasMany(r => r.Ingredients)
			.WithOne(i => i.Recipe)
			.HasForeignKey(i => i.RecipeId);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.Server)
			.WithMany(s => s.Recipes)
			.HasForeignKey(r => r.ServerId);

		// Product
		modelBuilder.Entity<Product>()
			.HasOne(p => p.Recipe)
			.WithMany(r => r.Products)
			.HasForeignKey(p => p.RecipeId);

		modelBuilder.Entity<Product>()
			.HasOne(p => p.ItemOrTag)
			.WithMany(iot => iot.Products)
			.HasForeignKey(p => p.ItemOrTagId);

		modelBuilder.Entity<Product>()
			.HasOne(p => p.Server)
			.WithMany(s => s.Products)
			.HasForeignKey(p => p.ServerId);

		// Ingredient
		modelBuilder.Entity<Ingredient>()
			.HasOne(i => i.Recipe)
			.WithMany(r => r.Ingredients)
			.HasForeignKey(i => i.RecipeId);

		modelBuilder.Entity<Ingredient>()
			.HasOne(i => i.ItemOrTag)
			.WithMany(iot => iot.Ingredients)
			.HasForeignKey(i => i.ItemOrTagId);

		modelBuilder.Entity<Ingredient>()
			.HasOne(i => i.Server)
			.WithMany(s => s.Ingredients)
			.HasForeignKey(i => i.ServerId);

		// ItemOrTag
		modelBuilder.Entity<ItemOrTag>()
			.HasMany(iot => iot.Products)
			.WithOne(p => p.ItemOrTag)
			.HasForeignKey(p => p.ItemOrTagId);

		modelBuilder.Entity<ItemOrTag>()
			.HasMany(iot => iot.Ingredients)
			.WithOne(i => i.ItemOrTag)
			.HasForeignKey(i => i.ItemOrTagId);

		modelBuilder.Entity<ItemOrTag>()
			.HasMany(iot => iot.UserPrices)
			.WithOne(up => up.ItemOrTag)
			.HasForeignKey(up => up.ItemOrTagId);

		modelBuilder.Entity<ItemOrTag>()
			.HasMany(iot => iot.ItemTagAssocs)
			.WithOne(ita => ita.Item)
			.HasForeignKey(ita => ita.ItemId);

		// UserPrice
		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.ItemOrTag)
			.WithMany(iot => iot.UserPrices)
			.HasForeignKey(up => up.ItemOrTagId);

		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.Server)
			.WithMany(s => s.UserPrices)
			.HasForeignKey(up => up.ServerId);

		// ItemTagAssoc
		modelBuilder.Entity<ItemTagAssoc>()
			.HasOne(ita => ita.Item)
			.WithMany(iot => iot.ItemTagAssocs)
			.HasForeignKey(ita => ita.ItemId);

		modelBuilder.Entity<ItemTagAssoc>()
			.HasOne(ita => ita.Tag)
			.WithMany(iot => iot.ItemTagAssocs)
			.HasForeignKey(ita => ita.TagId);

		modelBuilder.Entity<ItemTagAssoc>()
			.HasOne(ita => ita.Server)
			.WithMany(s => s.ItemTagAssocs)
			.HasForeignKey(ita => ita.ServerId);
	}

}
