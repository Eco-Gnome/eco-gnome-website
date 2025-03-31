using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserShoppingListRecipeDbService : IGenericUserDbService<UserShoppingListRecipe>
{
    private readonly EcoCraftDbContext context;

    public UserShoppingListRecipeDbService(EcoCraftDbContext context)
    {
        this.context = context;
    }

    public Task<List<UserShoppingListRecipe>> GetAllAsync()
    {
        return context.UserShoppingListRecipes
            .ToListAsync();
    }

    public async Task<List<UserShoppingListRecipe>> GetByUserServerAsync(UserServer userServer)
    {
        // Ici, on doit passer par la ShoppingList pour filtrer
        return await context.UserShoppingListRecipes
            .Include(r => r.UserShoppingList)
            .Where(r => r.UserShoppingList.UserServerId == userServer.Id)
            .ToListAsync();
    }

    public Task<UserShoppingListRecipe?> GetByIdAsync(Guid id)
    {
        return context.UserShoppingListRecipes
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public UserShoppingListRecipe Add(UserShoppingListRecipe recipe)
    {
        context.UserShoppingListRecipes.Add(recipe);
        return recipe;
    }

    public void Update(UserShoppingListRecipe recipe)
    {
        context.UserShoppingListRecipes.Update(recipe);
    }

    public void Delete(UserShoppingListRecipe recipe)
    {
        context.UserShoppingListRecipes.Remove(recipe);
    }
}
