using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserShoppingListDbService : IGenericUserDbService<UserShoppingList>
{
    private readonly EcoCraftDbContext context;

    public UserShoppingListDbService(EcoCraftDbContext context)
    {
        this.context = context;
    }

    public Task<List<UserShoppingList>> GetAllAsync()
    {
        return context.UserShoppingLists
            .ToListAsync();
    }

    public Task<List<UserShoppingList>> GetByUserServerAsync(UserServer userServer)
    {
        return context.UserShoppingLists
            .Where(s => s.UserServerId == userServer.Id)
            .ToListAsync();
    }

    public Task<UserShoppingList?> GetByIdAsync(Guid id)
    {
        return context.UserShoppingLists
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public UserShoppingList Add(UserShoppingList shoppingList)
    {
        context.UserShoppingLists.Add(shoppingList);
        return shoppingList;
    }

    public void Update(UserShoppingList shoppingList)
    {
        context.UserShoppingLists.Update(shoppingList);
    }

    public void Delete(UserShoppingList shoppingList)
    {
        context.UserShoppingLists.Remove(shoppingList);
    }
}
