using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ShoppingListDbService(EcoCraftDbContext context) : IGenericUserDbService<ShoppingList>
{
    public Task<List<ShoppingList>> GetAllAsync()
    {
        return context.ShoppingLists
            .ToListAsync();
    }

    public Task<List<ShoppingList>> GetByUserServerAsync(UserServer userServer)
    {
        return context.ShoppingLists
            .Where(s => s.UserServerId == userServer.Id)
            .ToListAsync();
    }

    public Task<ShoppingList?> GetByIdAsync(Guid id)
    {
        return context.ShoppingLists
            .Include(sl => sl.ShoppingListRecipes)
            .ThenInclude(slr => slr.ShoppingListItemOrTags)
            .Include(sl => sl.ShoppingListSkills)
            .Include(sl => sl.ShoppingListCraftingTables)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public ShoppingList Add(ShoppingList shoppingListCraftingTable)
    {
        context.ShoppingLists.Add(shoppingListCraftingTable);
        return shoppingListCraftingTable;
    }

    public void Update(ShoppingList shoppingList)
    {
        context.ShoppingLists.Update(shoppingList);
    }

    public void Delete(ShoppingList shoppingList)
    {
        context.ShoppingLists.Remove(shoppingList);
    }
}
