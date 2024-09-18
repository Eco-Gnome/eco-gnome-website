using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Models
{
    public class EcoCraftDbContext : DbContext
    {
        public EcoCraftDbContext(DbContextOptions<EcoCraftDbContext> options) : base(options) { }

        // DbSets for each entity
        public DbSet<User> Users { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<UserSkill> UserSkills { get; set; }
        public DbSet<CraftingTable> CraftingTables { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeMaterial> RecipeMaterials { get; set; }
        public DbSet<RecipeOutput> RecipeOutputs { get; set; }
        public DbSet<Upgrade> Upgrades { get; set; }
        public DbSet<CraftingTableUpgrade> CraftingTableUpgrades { get; set; }
        public DbSet<UserInputPrice> UserInputPrices { get; set; }
        public DbSet<UserCraftingTable> UserCraftingTables { get; set; }
        public DbSet<CraftingTableSkill> CraftingTableSkills { get; set; }

        // Configuring relationships and model properties
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // UserSkill: Many-to-Many relationship between User and Skill
            modelBuilder.Entity<UserSkill>()
                .HasOne(us => us.User)
                .WithMany(u => u.UserSkills)
                .HasForeignKey(us => us.UserId);

            modelBuilder.Entity<UserSkill>()
                .HasOne(us => us.Skill)
                .WithMany(s => s.UserSkills)
                .HasForeignKey(us => us.SkillId);

            // Recipe: One-to-Many relationship between Recipe and CraftingTable
            modelBuilder.Entity<Recipe>()
                .HasOne(r => r.CraftingTable)
                .WithMany(ct => ct.Recipes)
                .HasForeignKey(r => r.CraftingTableId);

            // Recipe: One-to-Many relationship between Recipe and Skill
            modelBuilder.Entity<Recipe>()
                .HasOne(r => r.Skill)
                .WithMany(s => s.Recipes)
                .HasForeignKey(r => r.SkillId);

            // RecipeMaterial: One-to-Many relationship between RecipeMaterial and Recipe
            modelBuilder.Entity<RecipeMaterial>()
                .HasOne(rm => rm.Recipe)
                .WithMany(r => r.RecipeMaterials)
                .HasForeignKey(rm => rm.RecipeId);

            // RecipeOutput: One-to-Many relationship between RecipeOutput and Recipe
            modelBuilder.Entity<RecipeOutput>()
                .HasOne(ro => ro.Recipe)
                .WithMany(r => r.RecipeOutputs)
                .HasForeignKey(ro => ro.RecipeId);

            // CraftingTableUpgrade: Many-to-Many between CraftingTable and Upgrade
            modelBuilder.Entity<CraftingTableUpgrade>()
                .HasOne(ctu => ctu.CraftingTable)
                .WithMany(ct => ct.CraftingTableUpgrades)
                .HasForeignKey(ctu => ctu.CraftingTableId);

            modelBuilder.Entity<CraftingTableUpgrade>()
                .HasOne(ctu => ctu.Upgrade)
                .WithMany(u => u.CraftingTableUpgrades)
                .HasForeignKey(ctu => ctu.UpgradeId);

            // UserInputPrice: Many-to-One between User and UserInputPrice
            modelBuilder.Entity<UserInputPrice>()
                .HasOne(uip => uip.User)
                .WithMany(u => u.UserInputPrices)
                .HasForeignKey(uip => uip.UserId);

            // Configuration de la relation many-to-many entre Skill et CraftingTable via CraftingTableSkill
            modelBuilder.Entity<CraftingTableSkill>()
                .HasKey(cts => new { cts.CraftingTableId, cts.SkillId }); // Clé composite

            modelBuilder.Entity<CraftingTableSkill>()
                .HasOne(cts => cts.CraftingTable)
                .WithMany(ct => ct.CraftingTableSkills)
                .HasForeignKey(cts => cts.CraftingTableId);

            modelBuilder.Entity<CraftingTableSkill>()
                .HasOne(cts => cts.Skill)
                .WithMany(s => s.CraftingTableSkills)
                .HasForeignKey(cts => cts.SkillId);
        }
    }
}
