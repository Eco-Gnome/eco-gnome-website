using ecocraft.Models;

namespace ecocraft.Services
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	public interface IGenericUserDbService<T> : IGenericDbService<T> where T : class
	{
		Task<List<T>> GetByUserServerAsync(UserServer userServer);
	}

}