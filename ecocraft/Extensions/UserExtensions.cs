using ecocraft.Models;

namespace ecocraft.Extensions
{
	public static class UserExtensions
	{
		// Méthode pour calculer les recettes disponibles
		public static IEnumerable<Recipe> GetAvailableRecipes(this User @this)
		{
			var skills = @this.UserSkills.Select(us => us.Skill);
			var craftingTables = @this.UserCraftingTables.Select(uct => uct.CraftingTable);
			var recipes = new HashSet<Recipe>();

			// Ajouter les recettes en fonction des compétences
			foreach (var skill in skills)
			{
				recipes.UnionWith(skill.Recipes);
			}

			// Ajouter les recettes en fonction des tables d'artisanat
			foreach (var table in craftingTables)
			{
				recipes.UnionWith(table.Recipes);
			}

			return recipes;
		}
	}
}
