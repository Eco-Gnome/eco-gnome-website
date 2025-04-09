namespace ecocraft.Services.DbServices;

public interface IGenericDbService<T> where T : class
{
	Task<List<T>> GetAllAsync();
	Task<T?> GetByIdAsync(Guid id);
	T Add(T talent);
	void Update(T entity);
	void Delete(T entity);
}