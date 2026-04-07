using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public interface IGenericNamedDbService<T> : IGenericDbService<T> where T : class
{
	Task<T?> GetByNameAsync(string name);
	Task<T?> GetByNameAsync(string name, EcoCraftDbContext context);
}
