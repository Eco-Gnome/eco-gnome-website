using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserServerDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<UserServer>
{
	public async Task<List<UserServer>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserServer>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserServers
			.ToListAsync();
	}

	public async Task<UserServer?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserServer?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserServers
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	public async Task<int> CountByServerIdAsync(Guid serverId)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await context.UserServers.CountAsync(us => us.ServerId == serverId);
	}

	private UserServer CloneForDb(UserServer userServer)
	{
		return new UserServer
		{
			Id = userServer.Id,
			Pseudo = userServer.Pseudo,
			EcoUserId = userServer.EcoUserId,
			IsAdmin = userServer.IsAdmin,
			UserId = userServer.User.Id,
			ServerId = userServer.Server.Id,
		};
	}

	public void Create(EcoCraftDbContext context, UserServer userServer)
	{
		context.Add(CloneForDb(userServer));
	}

	public void UpdateAll(EcoCraftDbContext context, UserServer userServer)
	{
		context.Attach(CloneForDb(userServer)).State = EntityState.Modified;
	}

	public void UpdateEcoUserIdAndPseudo(EcoCraftDbContext context, UserServer userServer)
	{
		var stub = new UserServer { Id = userServer.Id, EcoUserId = userServer.EcoUserId, Pseudo = userServer.Pseudo };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.EcoUserId).IsModified = true;
		entry.Property(x => x.Pseudo).IsModified = true;
	}

	public void UpdateIsAdmin(EcoCraftDbContext context, UserServer userServer)
	{
		var stub = new UserServer { Id = userServer.Id, IsAdmin = userServer.IsAdmin };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.IsAdmin).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserServer userServer)
	{
		var entity = new UserServer { Id = userServer.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
