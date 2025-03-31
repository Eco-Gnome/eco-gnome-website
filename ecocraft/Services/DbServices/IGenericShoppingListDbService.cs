using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public interface IGenericShoppingListDbService<T> : IGenericDbService<T> where T : class
{
	Task<List<T>> GetByShoppingListAsync(ShoppingList shoppingList);
}
