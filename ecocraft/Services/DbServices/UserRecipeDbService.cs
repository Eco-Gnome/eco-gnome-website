using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserRecipeDbService(EcoCraftDbContext context) : IGenericUserDbService<UserRecipe>
{
    public Task<List<UserRecipe>> GetAllAsync()
    {
        return context.UserRecipes
            .ToListAsync();
    }

    public Task<List<UserRecipe>> GetByDataContextAsync(DataContext dataContext)
    {
        return context.UserRecipes
            .Where(s => s.DataContextId == dataContext.Id)
            .ToListAsync();
    }

    public Task<UserRecipe?> GetByIdAsync(Guid id)
    {
        return context.UserRecipes
            .FirstOrDefaultAsync(up => up.Id == id);
    }

    public UserRecipe Add(UserRecipe talent)
    {
        context.UserRecipes.Add(talent);
        talent.Recipe.CurrentUserRecipe = talent;

        return talent;
    }

    public void Update(UserRecipe userRecipe)
    {
        context.UserRecipes.Update(userRecipe);
    }

    public void Delete(UserRecipe userRecipe)
    {
        userRecipe.Recipe.CurrentUserRecipe = null;

        context.UserRecipes.Remove(userRecipe);
    }
}
