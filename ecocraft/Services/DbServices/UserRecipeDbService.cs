using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserRecipeDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserRecipe>
{
    public async Task<List<UserRecipe>> GetAllAsync(EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.UserRecipes
            .ToListAsync();
    }

    public async Task<List<UserRecipe>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.UserRecipes
            .Where(s => s.DataContextId == dataContext.Id)
            .Include(r => r.UserElements)
            .ToListAsync();
    }

    public async Task<List<UserRecipe>> GetByDataContextForEcoApiAsync(DataContext dataContext, EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.UserRecipes
            .Where(ur => ur.DataContextId == dataContext.Id)
            .Include(ur => ur.Recipe)
            .ThenInclude(r => r.Elements)
            .ThenInclude(e => e.ItemOrTag)
            .Include(ur => ur.Recipe)
            .ThenInclude(r => r.Skill)
            .ToListAsync();
    }

    public async Task<UserRecipe?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.UserRecipes
            .FirstOrDefaultAsync(up => up.Id == id);
    }

    private UserRecipe CloneForDb(UserRecipe userRecipe)
    {
        return new UserRecipe
        {
            Id = userRecipe.Id,
            RecipeId = userRecipe.Recipe.Id,
            DataContextId = userRecipe.DataContext.Id,
            RoundFactor = userRecipe.RoundFactor,
            LockShare = userRecipe.LockShare,
            ParentUserRecipeId = userRecipe.ParentUserRecipe?.Id,
        };
    }

    public void Create(EcoCraftDbContext context, UserRecipe userRecipe)
    {
        context.Add(CloneForDb(userRecipe));
    }

    public void UpdateAll(EcoCraftDbContext context, UserRecipe userRecipe)
    {
        context.Attach(CloneForDb(userRecipe)).State = EntityState.Modified;
    }

    public void UpdateRoundFactor(EcoCraftDbContext context, UserRecipe userRecipe)
    {
        var entry = context.Attach(userRecipe);
        entry.Property(x => x.RoundFactor).IsModified = true;
    }

    public void Destroy(EcoCraftDbContext context, UserRecipe userRecipe)
    {
        var entity = new UserRecipe { Id = userRecipe.Id };
        context.Entry(entity).State = EntityState.Deleted;
    }
}
