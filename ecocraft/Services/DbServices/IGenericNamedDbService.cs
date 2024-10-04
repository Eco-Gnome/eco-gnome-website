namespace ecocraft.Services
{
	using System.Threading.Tasks;

	public interface IGenericNamedDbService<T> : IGenericDbService<T> where T : class
	{
		Task<T?> GetByNameAsync(string name);
	}
}