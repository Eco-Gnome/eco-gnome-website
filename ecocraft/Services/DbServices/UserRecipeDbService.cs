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

    public Task<List<UserRecipe>> GetByDataContextForEcoApiAsync(DataContext dataContext)
    {
        return context.UserRecipes
            .Where(ur => ur.DataContextId == dataContext.Id)
            .Include(ur => ur.Recipe)
            .ThenInclude(r => r.Elements)
            .ThenInclude(e => e.ItemOrTag)
            .Include(ur => ur.Recipe)
            .ThenInclude(r => r.Skill)
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
        return talent;
    }

    public void Update(UserRecipe userRecipe)
    {
        context.UserRecipes.Update(userRecipe);
    }

    public void Delete(UserRecipe userRecipe)
    {
        context.UserRecipes.Remove(userRecipe);
    }
}
