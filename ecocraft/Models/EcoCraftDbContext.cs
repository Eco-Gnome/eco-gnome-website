using Blazor.Diagrams.Core.Anchors;
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
	public DbSet<Talent> Talents { get; set; }
	public DbSet<DynamicValue> DynamicValues { get; set; }
	public DbSet<Modifier> Modifiers { get; set; }
	public DbSet<CraftingTable> CraftingTables { get; set; }
	public DbSet<PluginModule> PluginModules { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<UserServer> UserServers { get; set; }
	public DbSet<DataContext> DataContexts { get; set; }
	public DbSet<UserSetting> UserSettings { get; set; }
	public DbSet<UserCraftingTable> UserCraftingTables { get; set; }
	public DbSet<UserSkill> UserSkills { get; set; }
	public DbSet<UserTalent> UserTalents { get; set; }
	public DbSet<UserElement> UserElements { get; set; }
	public DbSet<UserPrice> UserPrices { get; set; }
	public DbSet<UserRecipe> UserRecipes { get; set; }
	public DbSet<Server> Servers { get; set; }
    public DbSet<UserMargin> UserMargins { get; set; }
    public DbSet<ModUploadHistory> ModUploadHistories { get; set; }

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
			.HasForeignKey(r => r.SkillId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.CraftingTable)
			.WithMany(ct => ct.Recipes)
			.HasForeignKey(r => r.CraftingTableId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.Server)
			.WithMany(s => s.Recipes)
			.HasForeignKey(r => r.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.LocalizedName)
			.WithMany(lt => lt.Recipes)
			.HasForeignKey(r => r.LocalizedNameId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.CraftMinutes)
			.WithMany(lt => lt.CraftMinutesRecipes)
			.HasForeignKey(lt => lt.CraftMinutesId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Recipe>()
			.HasOne(r => r.Labor)
			.WithMany(lt => lt.LaborRecipes)
			.HasForeignKey(lt => lt.LaborId)
			.OnDelete(DeleteBehavior.Cascade);

		// Element
		modelBuilder.Entity<Element>()
			.ToTable("Element");

		modelBuilder.Entity<Element>()
			.HasOne(e => e.Recipe)
			.WithMany(r => r.Elements)
			.HasForeignKey(e => e.RecipeId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Element>()
			.HasOne(e => e.ItemOrTag)
			.WithMany(i => i.Elements)
			.HasForeignKey(e => e.ItemOrTagId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Element>()
			.HasOne(e => e.Quantity)
			.WithMany(i => i.QuantityElements)
			.HasForeignKey(e => e.QuantityId)
			.OnDelete(DeleteBehavior.Cascade);

		//DynamicValue
		modelBuilder.Entity<DynamicValue>()
			.ToTable("DynamicValue");

		modelBuilder.Entity<DynamicValue>()
			.HasOne(dv => dv.Server)
			.WithMany(s => s.DynamicValues)
			.HasForeignKey(dv => dv.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		//Modifier
		modelBuilder.Entity<Modifier>()
			.ToTable("Modifier");

		modelBuilder.Entity<Modifier>()
			.HasOne(m => m.DynamicValue)
			.WithMany(dv => dv.Modifiers)
			.HasForeignKey(m => m.DynamicValueId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Modifier>()
			.HasOne(m => m.Skill)
			.WithMany(dv => dv.Modifiers)
			.HasForeignKey(m => m.SkillId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Modifier>()
			.HasOne(m => m.Talent)
			.WithMany(dv => dv.Modifiers)
			.HasForeignKey(m => m.TalentId)
			.OnDelete(DeleteBehavior.Cascade);

		// ItemOrTag
		modelBuilder.Entity<ItemOrTag>()
			.ToTable("ItemOrTag");

		modelBuilder.Entity<ItemOrTag>()
			.HasOne(i => i.Server)
			.WithMany(s => s.ItemOrTags)
			.HasForeignKey(i => i.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<ItemOrTag>()
			.HasMany(i => i.AssociatedTags)
			.WithMany(i => i.AssociatedItems)
			.UsingEntity(
				"ItemTagAssoc",
				r => r.HasOne(typeof(ItemOrTag))
					.WithMany()
					.HasForeignKey("ItemId")
					.OnDelete(DeleteBehavior.Cascade)
					.HasPrincipalKey(nameof(ItemOrTag.Id)),
				l => l.HasOne(typeof(ItemOrTag))
					.WithMany()
					.HasForeignKey("TagId")
					.OnDelete(DeleteBehavior.Cascade)
					.HasPrincipalKey(nameof(ItemOrTag.Id)),
				j => j.HasKey("ItemId", "TagId"));

		modelBuilder.Entity<ItemOrTag>()
			.HasOne(i => i.LocalizedName)
			.WithMany(lt => lt.ItemOrTags)
			.HasForeignKey(i => i.LocalizedNameId)
			.OnDelete(DeleteBehavior.Cascade);

		// Skill
		modelBuilder.Entity<Skill>()
			.ToTable("Skill");

		modelBuilder.Entity<Skill>()
			.HasOne(s => s.Server)
			.WithMany(s => s.Skills)
			.HasForeignKey(s => s.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Skill>()
			.HasOne(s => s.LocalizedName)
			.WithMany(lt => lt.Skills)
			.HasForeignKey(s => s.LocalizedNameId)
			.OnDelete(DeleteBehavior.Cascade);

		// Talent
		modelBuilder.Entity<Talent>()
			.ToTable("Talent");

		modelBuilder.Entity<Talent>()
			.HasOne(s => s.LocalizedName)
			.WithMany(lt => lt.Talents)
			.HasForeignKey(s => s.LocalizedNameId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<Talent>()
			.HasOne(s => s.Skill)
			.WithMany(lt => lt.Talents)
			.HasForeignKey(s => s.SkillId)
			.OnDelete(DeleteBehavior.Cascade);

		// CraftingTable
		modelBuilder.Entity<CraftingTable>()
			.ToTable("CraftingTable");

		modelBuilder.Entity<CraftingTable>()
			.HasOne(s => s.Server)
			.WithMany(s => s.CraftingTables)
			.HasForeignKey(s => s.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<CraftingTable>()
			.HasMany(ct => ct.PluginModules)
			.WithMany(pm => pm.CraftingTables)
			.UsingEntity(
				"CraftingTablePluginModule",
				r => r.HasOne(typeof(PluginModule))
					.WithMany()
					.HasForeignKey("PluginModuleId")
					.OnDelete(DeleteBehavior.Cascade)
					.HasPrincipalKey(nameof(PluginModule.Id)),
				l => l.HasOne(typeof(CraftingTable))
					.WithMany()
					.HasForeignKey("CraftingTableId")
					.OnDelete(DeleteBehavior.Cascade)
					.HasPrincipalKey(nameof(CraftingTable.Id)),
				j => j.HasKey("CraftingTableId", "PluginModuleId"));

		modelBuilder.Entity<CraftingTable>()
			.HasOne(c => c.LocalizedName)
			.WithMany(lt => lt.CraftingTables)
			.HasForeignKey(c => c.LocalizedNameId)
			.OnDelete(DeleteBehavior.Cascade);

		// PluginModule
		modelBuilder.Entity<PluginModule>()
			.ToTable("PluginModule");

		modelBuilder.Entity<PluginModule>()
			.HasOne(pm => pm.Server)
			.WithMany(s => s.PluginModules)
			.HasForeignKey(pm => pm.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<PluginModule>()
			.HasOne(pm => pm.LocalizedName)
			.WithMany(lt => lt.PluginModules)
			.HasForeignKey(pm => pm.LocalizedNameId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<PluginModule>()
			.HasOne(pm => pm.Skill)
			.WithMany(s => s.PluginModules)
			.HasForeignKey(pm => pm.SkillId)
			.OnDelete(DeleteBehavior.Cascade);

		// * User Data
		// User
		modelBuilder.Entity<User>()
			.ToTable("User");

		// DataContext
		modelBuilder.Entity<DataContext>()
			.ToTable("DataContext");

		modelBuilder.Entity<DataContext>()
			.HasOne(us => us.UserServer)
			.WithMany(us => us.DataContexts)
			.HasForeignKey(us => us.UserServerId)
			.OnDelete(DeleteBehavior.Cascade);

		// UserSetting
		modelBuilder.Entity<UserSetting>()
			.ToTable("UserSetting");

		modelBuilder.Entity<UserSetting>()
			.HasOne(us => us.DataContext)
			.WithMany(us => us.UserSettings)
			.HasForeignKey(us => us.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

        // UserMargin
        modelBuilder.Entity<UserMargin>()
            .ToTable("UserMargin");

        modelBuilder.Entity<UserMargin>()
            .HasOne(us => us.DataContext)
            .WithMany(us => us.UserMargins)
            .HasForeignKey(us => us.DataContextId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserCraftingTable
        modelBuilder.Entity<UserCraftingTable>()
			.ToTable("UserCraftingTable");

		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.DataContext)
			.WithMany(us => us.UserCraftingTables)
			.HasForeignKey(uct => uct.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.CraftingTable)
			.WithMany(u => u.UserCraftingTables)
			.HasForeignKey(uct => uct.CraftingTableId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserCraftingTable>()
			.HasOne(uct => uct.PluginModule)
			.WithMany()
			.HasForeignKey(s => s.PluginModuleId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired(false);

		modelBuilder.Entity<UserCraftingTable>()
			.HasMany(uct => uct.SkilledPluginModules)
			.WithMany(pm => pm.UserCraftingTables)
			.UsingEntity(
				"UserCraftingTablePluginModule",
				r => r.HasOne(typeof(PluginModule))
					.WithMany()
					.HasForeignKey("PluginModuleId")
					.OnDelete(DeleteBehavior.Cascade)
					.HasPrincipalKey(nameof(PluginModule.Id)),
				l => l.HasOne(typeof(UserCraftingTable))
					.WithMany()
					.HasForeignKey("UserCraftingTableId")
					.OnDelete(DeleteBehavior.Cascade)
					.HasPrincipalKey(nameof(UserCraftingTable.Id)),
				j => j.HasKey("UserCraftingTableId", "PluginModuleId"));

		// UserSkill
		modelBuilder.Entity<UserSkill>()
			.ToTable("UserSkill");

		modelBuilder.Entity<UserSkill>()
			.HasOne(us => us.Skill)
			.WithMany(s => s.UserSkills)
			.HasForeignKey(us => us.SkillId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserSkill>()
			.HasOne(us => us.DataContext)
			.WithMany(use => use.UserSkills)
			.HasForeignKey(us => us.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

		// UserTalent
		modelBuilder.Entity<UserTalent>()
			.ToTable("UserTalent");

		modelBuilder.Entity<UserTalent>()
			.HasOne(ut => ut.Talent)
			.WithMany(t => t.UserTalents)
			.HasForeignKey(ut => ut.TalentId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserTalent>()
			.HasOne(ut => ut.DataContext)
			.WithMany(use => use.UserTalents)
			.HasForeignKey(ut => ut.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

		// UserElement
		modelBuilder.Entity<UserElement>()
			.ToTable("UserElement");

		modelBuilder.Entity<UserElement>()
			.HasOne(ue => ue.Element)
			.WithMany(e => e.UserElements)
			.HasForeignKey(ue => ue.ElementId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserElement>()
			.HasOne(ue => ue.DataContext)
			.WithMany(us => us.UserElements)
			.HasForeignKey(ue => ue.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

		// UserPrice
		modelBuilder.Entity<UserPrice>()
			.ToTable("UserPrice");

		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.UserMargin)
			.WithMany(um => um.UserPrices)
			.HasForeignKey(up => up.UserMarginId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.ItemOrTag)
			.WithMany(iot => iot.UserPrices)
			.HasForeignKey(up => up.ItemOrTagId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.DataContext)
			.WithMany(us => us.UserPrices)
			.HasForeignKey(up => up.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.PrimaryUserElement)
			.WithMany()
			.HasForeignKey(up => up.PrimaryUserElementId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserPrice>()
			.HasOne(up => up.PrimaryUserPrice)
			.WithMany()
			.HasForeignKey(up => up.PrimaryUserPriceId)
			.OnDelete(DeleteBehavior.Cascade);

		// UserRecipe
		modelBuilder.Entity<UserRecipe>()
			.ToTable("UserRecipe");

		modelBuilder.Entity<UserRecipe>()
			.HasOne(up => up.Recipe)
			.WithMany(r => r.UserRecipes)
			.HasForeignKey(up => up.RecipeId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserRecipe>()
			.HasOne(ur => ur.DataContext)
			.WithMany(us => us.UserRecipes)
			.HasForeignKey(ur => ur.DataContextId)
			.OnDelete(DeleteBehavior.Cascade);

		// UserServer
		modelBuilder.Entity<UserServer>()
			.ToTable("UserServer");

		modelBuilder.Entity<UserServer>()
			.HasOne(us => us.User)
			.WithMany(u => u.UserServers)
			.HasForeignKey(us => us.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserServer>()
			.HasOne(us => us.Server)
			.WithMany(s => s.UserServers)
			.HasForeignKey(us => us.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		// * Server Data
		// Server
		modelBuilder.Entity<Server>()
			.ToTable("Server");

		// * History
		// ModUploadHistory

		modelBuilder.Entity<ModUploadHistory>()
			.ToTable("ModUploadHistory");

		modelBuilder.Entity<ModUploadHistory>()
			.HasOne(muh => muh.User)
			.WithMany(u => u.ModUploadHistories)
			.HasForeignKey(muh => muh.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<ModUploadHistory>()
			.HasOne(muh => muh.Server)
			.WithMany(u => u.ModUploadHistories)
			.HasForeignKey(muh => muh.ServerId)
			.OnDelete(DeleteBehavior.Cascade);

		// * Utils
		// LocalizedField
		modelBuilder.Entity<LocalizedField>()
			.ToTable("LocalizedField");

		modelBuilder.Entity<LocalizedField>()
			.HasOne(lf => lf.Server)
			.WithMany()
			.HasForeignKey(lf => lf.ServerId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
