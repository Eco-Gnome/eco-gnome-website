namespace ecocraft.Services
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	public interface IGenericService<T> where T : class
	{
		Task<List<T>> GetAllAsync();
		Task<T> GetByIdAsync(Guid id);
		Task AddAsync(T entity);
		Task UpdateAsync(T entity);
		Task DeleteAsync(Guid id);
	}

}