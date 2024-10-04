namespace ecocraft.Services
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	public interface IGenericDbService<T> where T : class
	{
		Task<List<T>> GetAllAsync();
		Task<T?> GetByIdAsync(Guid id);
		T Add(T entity);
		void Update(T entity);
		void Delete(T entity);
	}

}