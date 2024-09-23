using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

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
	public DbSet<UserSetting> UserSettings { get; set; }
	public DbSet<UserCraftingTable> UserCraftingTables { get; set; }
	public DbSet<UserSkill> UserSkills { get; set; }
	public DbSet<UserElement> UserElements { get; set; }
	public DbSet<UserPrice> UserPrices { get; set; }
	public DbSet<Server> Servers { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		
		// * Eco Data
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
			.HasOne(r => r.Server)
			.WithMany(s => s.Recipes)
			.HasForeignKey(r => r.ServerId);
		
		// Element
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
			.HasOne(i => i.Server)
			.WithMany(s => s.ItemOrTags)
			.HasForeignKey(i => i.ServerId);
		
		modelBuilder.Entity<ItemOrTag>()
			.HasMany(iot => iot.AssociatedItemOrTags)
			.WithMany()
			.UsingEntity(
				"ItemTagAssoc",
				l => l.HasOne(typeof(ItemOrTag)).WithMany().HasForeignKey("TagId")
					.HasPrincipalKey(nameof(ItemOrTag.Id)),
				r => r.HasOne(typeof(ItemOrTag)).WithMany().HasForeignKey("ItemId")
					.HasPrincipalKey(nameof(ItemOrTag.Id)),
				j => j.HasKey("TagId", "ItemId"));

		// Skill
		modelBuilder.Entity<Skill>()
			.HasOne(s => s.Server)
			.WithMany(s => s.Skills)
			.HasForeignKey(s => s.ServerId);
		
		// CraftingTable
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
			.HasOne(s => s.Server)
			.WithMany(s => s.PluginModules)
			.HasForeignKey(s => s.ServerId);
		
		// * User Data
		// User
		modelBuilder.Entity<User>()
			.HasMany(u => u.Servers)
			.WithMany(s => s.Users)
			.UsingEntity(
				"UserServer",
				r => r.HasOne(typeof(Server)).WithMany().HasForeignKey("ServerId")
					.HasPrincipalKey(nameof(Server.Id)),
				l => l.HasOne(typeof(User)).WithMany().HasForeignKey("UserId")
					.HasPrincipalKey(nameof(User.Id)),
				j => j.HasKey("UserId", "ServerId"));
		
		// UserSetting
		modelBuilder.Entity<UserSetting>()
			.HasOne(us => us.User)
			.WithMany(u => u.UserSettings)
			.HasForeignKey(us => us.UserId);
		
		modelBuilder.Entity<UserSetting>()
			.HasOne(us => us.Server)
			.WithMany(s => s.UserSettings)
			.HasForeignKey(us => us.ServerId);
		
		// UserCraftingTable
		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.User)
			.WithMany(u => u.UserCraftingTables)
			.HasForeignKey(uct => uct.UserId);
		
		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.CraftingTable)
			.WithMany(u => u.UserCraftingTables)
			.HasForeignKey(uct => uct.CraftingTableId);
		
		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(s => s.PluginModule)
			.WithMany()
			.HasForeignKey(s => s.PluginModuleId);
		
		// UserSkill
		modelBuilder.Entity<UserSkill>()
			.HasOne(us => us.Skill)
			.WithMany(s => s.UserSkills)
			.HasForeignKey(us => us.SkillId);
		
		modelBuilder.Entity<UserSkill>()
			.HasOne(us => us.User)
			.WithMany(u => u.UserSkills)
			.HasForeignKey(us => us.UserId);
		
		// UserElement
		modelBuilder.Entity<UserElement>()
			.HasOne(ue => ue.Element)
			.WithMany(e => e.UserElements)
			.HasForeignKey(ue => ue.ElementId);
		
		modelBuilder.Entity<UserElement>()
			.HasOne(ue => ue.User)
			.WithMany(u => u.UserElements)
			.HasForeignKey(ue => ue.UserId);
		
		// UserPrice
		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.ItemOrTag)
			.WithMany(iot => iot.UserPrices)
			.HasForeignKey(ue => ue.ItemOrTagId);
		
		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.User)
			.WithMany(u => u.UserPrices)
			.HasForeignKey(ue => ue.UserId);
		
		// * Server Data
		// Server
	}

}
