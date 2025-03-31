using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public class ShoppingListItemOrTagDbService(EcoCraftDbContext context)
{
    public ShoppingListItemOrTag Add(ShoppingListItemOrTag shoppingListCraftingTable)
    {
        context.ShoppingListItemOrTags.Add(shoppingListCraftingTable);
        return shoppingListCraftingTable;
    }

    public void Update(ShoppingListItemOrTag element)
    {
        context.ShoppingListItemOrTags.Update(element);
    }

    public void Delete(ShoppingListItemOrTag element)
    {
        context.ShoppingListItemOrTags.Remove(element);
    }
}
