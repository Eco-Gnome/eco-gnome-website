using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public interface IGenericDbService<T> where T : class
{
	Task<List<T>> GetAllAsync(EcoCraftDbContext? context = null);
	Task<T?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null);
}
