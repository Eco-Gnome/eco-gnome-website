using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class DataContextDbService(EcoCraftDbContext context)
{
	public Task<List<DataContext>> GetAllAsync()
	{
		return context.DataContexts
			.ToListAsync();
	}

	public Task<List<DataContext>> GetByUserServerAsync(UserServer userServer)
	{
		return context.DataContexts
			.Where(s => s.UserServerId == userServer.Id)
			.ToListAsync();
	}

	public Task<DataContext?> GetByIdAsync(Guid id)
	{
		return context.DataContexts
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	public DataContext Add(DataContext dataContext)
	{
		context.DataContexts.Add(dataContext);

		return dataContext;
	}

	public void Update(DataContext dataContext)
	{
		context.DataContexts.Update(dataContext);
	}

	public void Delete(DataContext dataContext)
	{
		context.DataContexts.Remove(dataContext);
	}
}
