using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public interface IGenericDbService<T> where T : class
{
	Task<List<T>> GetAllAsync();
	Task<List<T>> GetAllAsync(EcoCraftDbContext context);
	Task<T?> GetByIdAsync(Guid id);
	Task<T?> GetByIdAsync(Guid id, EcoCraftDbContext context);
}
