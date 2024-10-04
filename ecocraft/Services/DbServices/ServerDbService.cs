using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ServerDbService(EcoCraftDbContext context) : IGenericDbService<Server>
{
	public async Task<List<Server>> GetAllAsync()
	{
		return await context.Servers.ToListAsync();
	}

	public async Task<List<Server>> GetAllDefaultAsync()
	{
		return await context.Servers.Where(s => s.IsDefault).ToListAsync();
	}

	public async Task<Server?> GetByIdAsync(Guid id)
	{
		return await context.Servers.Include(s => s.UserServers)
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public Server Add(Server server)
	{
		context.Servers.Add(server);

		return server;
	}

	public void Update(Server server)
	{
		context.Servers.Update(server);
	}

	public async Task<Server> AddAndSave(Server server)
	{
		context.Servers.Add(server);

		await context.SaveChangesAsync();

		return server;
	}

	public Task UpdateAndSave(Server server)
	{
		context.Servers.Update(server);
		
		return context.SaveChangesAsync();
	}

	public void Delete(Server server)
	{
		context.Servers.Remove(server);
	}
}