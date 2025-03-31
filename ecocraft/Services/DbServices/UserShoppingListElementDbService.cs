using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserShoppingListElementDbService : IGenericUserDbService<UserShoppingListItemOrTag>
{
    private readonly EcoCraftDbContext context;

    public UserShoppingListElementDbService(EcoCraftDbContext context)
    {
        this.context = context;
    }

    public Task<List<UserShoppingListItemOrTag>> GetAllAsync()
    {
        return context.UserShoppingListElements
            .ToListAsync();
    }

    public async Task<List<UserShoppingListItemOrTag>> GetByUserServerAsync(UserServer userServer)
    {
        // On passe par la ShoppingList pour filtrer sur UserServerId
        return await context.UserShoppingListElements
            .Include(e => e.UserShoppingList)
            .Where(e => e.UserShoppingList.UserServerId == userServer.Id)
            .ToListAsync();
    }

    public Task<UserShoppingListItemOrTag?> GetByIdAsync(Guid id)
    {
        return context.UserShoppingListElements
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public UserShoppingListItemOrTag Add(UserShoppingListItemOrTag element)
    {
        context.UserShoppingListElements.Add(element);
        return element;
    }

    public void Update(UserShoppingListItemOrTag element)
    {
        context.UserShoppingListElements.Update(element);
    }

    public void Delete(UserShoppingListItemOrTag element)
    {
        context.UserShoppingListElements.Remove(element);
    }
}
