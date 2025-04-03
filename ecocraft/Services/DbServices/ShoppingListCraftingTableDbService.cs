using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public class ShoppingListCraftingTableDbService(EcoCraftDbContext context)
{
    public ShoppingListCraftingTable Add(ShoppingListCraftingTable shoppingListCraftingTable)
    {
        context.ShoppingListCraftingTables.Add(shoppingListCraftingTable);
        return shoppingListCraftingTable;
    }

    public void Update(ShoppingListCraftingTable shoppingListCraftingTable)
    {
        context.ShoppingListCraftingTables.Update(shoppingListCraftingTable);
    }

    public void Delete(ShoppingListCraftingTable shoppingListCraftingTable)
    {
        context.ShoppingListCraftingTables.Remove(shoppingListCraftingTable);
    }
}
