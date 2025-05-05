using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ServerDbService(EcoCraftDbContext context) : IGenericDbService<Server>
{
	public async Task<List<Server>> GetAllAsync()
	{
		var servers = await context.Servers
			.Include(u => u.UserServers)
			.ThenInclude(us => us.User)
			.OrderByDescending(u => u.CreationDateTime)
			.ToListAsync();

		foreach (var server in servers)
			server.IsEmpty = !await context.Entry(server).Collection(s => s.Skills).Query().AnyAsync();

		return servers;
	}

	public Task<List<Server>> GetAllDefaultAsync()
	{
		return context.Servers
			.Where(s => s.IsDefault)
			.ToListAsync();
	}

	public Task<Server?> GetByIdAsync(Guid id)
	{
		return context.Servers
			.Include(s => s.UserServers)
			.ThenInclude(us => us.User)
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public Task<Server?> GetByEcoServerIdAsync(string ecoServerId)
	{
		return context.Servers
			.FirstOrDefaultAsync(s => s.EcoServerId == ecoServerId);
	}

    public Task<Server?> GetByJoinCodeAsync(string joinCode)
    {
        return context.Servers.FirstOrDefaultAsync(s => s.JoinCode == joinCode);
    }

    public Server Add(Server talent)
	{
		context.Servers.Add(talent);

		return talent;
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

	public async Task DeleteAsync(Server server, User user)
	{
		context.Entry(server).State = EntityState.Detached;
		context.Set<Server>().Local.Remove(server);
		await context.Entry(user).Collection(u => u.UserServers).LoadAsync();
		await context.Servers.Where(s => s.Id == server.Id).ExecuteDeleteAsync();
	}
}
