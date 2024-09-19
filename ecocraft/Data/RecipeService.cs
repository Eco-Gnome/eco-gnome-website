using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
	public class RecipeService
	{
		private readonly EcoCraftDbContext _dbContext;

		public RecipeService(EcoCraftDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		// Ajouter une recette
		public async Task<Recipe> AddRecipeAsync(Recipe recipe)
		{
			_dbContext.Recipes.Add(recipe);
			await _dbContext.SaveChangesAsync();
			return recipe;
		}

		// Obtenir une recette par son ID
		public async Task<Recipe> GetRecipeByIdAsync(int id)
		{
			return await _dbContext.Recipes
				.Include(r => r.Skill) // Inclut la compétence associée
				.Include(r => r.CraftingTable) // Inclut la table d'artisanat associée
				.Include(r => r.RecipeMaterials) // Inclut les matériaux de la recette
				.Include(r => r.RecipeOutputs) // Inclut les sorties de la recette
				.FirstOrDefaultAsync(r => r.Id == id);
		}

		// Obtenir toutes les recettes
		public async Task<List<Recipe>> GetAllRecipesAsync()
		{
			return await _dbContext.Recipes
				.Include(r => r.Skill)
				.Include(r => r.CraftingTable)
				.Include(r => r.RecipeMaterials)
				.Include(r => r.RecipeOutputs)
				.ToListAsync();
		}

		// Mettre à jour une recette
		public async Task<Recipe> UpdateRecipeAsync(Recipe updatedRecipe)
		{
			var existingRecipe = await _dbContext.Recipes
				.FirstOrDefaultAsync(r => r.Id == updatedRecipe.Id);

			if (existingRecipe != null)
			{
				existingRecipe.Name = updatedRecipe.Name;
				existingRecipe.CaloriesRequired = updatedRecipe.CaloriesRequired;
				existingRecipe.SkillId = updatedRecipe.SkillId;
				existingRecipe.MinimumSkillLevel = updatedRecipe.MinimumSkillLevel;
				existingRecipe.CraftingTableId = updatedRecipe.CraftingTableId;
				existingRecipe.RecipeMaterials = updatedRecipe.RecipeMaterials;
				existingRecipe.RecipeOutputs = updatedRecipe.RecipeOutputs;

				await _dbContext.SaveChangesAsync();
			}

			return existingRecipe;
		}

		// Supprimer une recette
		public async Task<bool> DeleteRecipeAsync(int id)
		{
			var recipe = await _dbContext.Recipes.FirstOrDefaultAsync(r => r.Id == id);
			if (recipe != null)
			{
				_dbContext.Recipes.Remove(recipe);
				await _dbContext.SaveChangesAsync();
				return true;
			}

			return false;
		}

	}
}
