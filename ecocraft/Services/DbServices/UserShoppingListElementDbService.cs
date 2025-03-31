using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserShoppingListElementDbService : IGenericUserDbService<UserShoppingListElement>
{
    private readonly EcoCraftDbContext context;

    public UserShoppingListElementDbService(EcoCraftDbContext context)
    {
        this.context = context;
    }

    public Task<List<UserShoppingListElement>> GetAllAsync()
    {
        return context.UserShoppingListElements
            .ToListAsync();
    }

    public async Task<List<UserShoppingListElement>> GetByUserServerAsync(UserServer userServer)
    {
        // On passe par la ShoppingList pour filtrer sur UserServerId
        return await context.UserShoppingListElements
            .Include(e => e.UserShoppingList)
            .Where(e => e.UserShoppingList.UserServerId == userServer.Id)
            .ToListAsync();
    }

    public Task<UserShoppingListElement?> GetByIdAsync(Guid id)
    {
        return context.UserShoppingListElements
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public UserShoppingListElement Add(UserShoppingListElement element)
    {
        context.UserShoppingListElements.Add(element);
        return element;
    }

    public void Update(UserShoppingListElement element)
    {
        context.UserShoppingListElements.Update(element);
    }

    public void Delete(UserShoppingListElement element)
    {
        context.UserShoppingListElements.Remove(element);
    }
}
