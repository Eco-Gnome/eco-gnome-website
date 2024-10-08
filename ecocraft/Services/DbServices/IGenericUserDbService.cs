using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public interface IGenericUserDbService<T> : IGenericDbService<T> where T : class
{
	Task<List<T>> GetByUserServerAsync(UserServer userServer);
}