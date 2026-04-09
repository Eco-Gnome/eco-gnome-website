using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public enum RegisterServerResult
{
	Success,
	EcoGnomeServerNotFound,
	EcoServerAlreadyRegisteredWithOtherEcoServer,
	EcoGnomeServerAlreadyLinkedToOtherEcoServer
}

public class RegisterServerResultInfo
{
	public RegisterServerResult Result { get; set; }
	public string? OtherServerName { get; set; }
}

public class ServerDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<Server>
{
	public async Task<List<Server>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<Server>> GetAllAsync(EcoCraftDbContext context)
	{
		var servers = await context.Servers
			.Include(u => u.UserServers)
			.ThenInclude(us => us.User)
			.Include(s => s.Skills)
			.OrderByDescending(u => u.CreationDateTime)
			.ToListAsync();

		foreach (var server in servers)
			server.IsEmpty = server.Skills.Count == 0;

		return servers;
	}

	public async Task<List<Server>> GetAllDefaultAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllDefaultAsync(context);
	}

	public async Task<List<Server>> GetAllDefaultAsync(EcoCraftDbContext context)
	{
		return await context.Servers
			.Where(s => s.IsDefault)
			.OrderByDescending(s => s.CreationDateTime)
			.ToListAsync();
	}

	public async Task<Server> GetServerWithData(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetServerWithData(id, context);
	}

	public async Task<Server> GetServerWithData(Guid id, EcoCraftDbContext context)
	{
		return await context.Servers
			.AsNoTrackingWithIdentityResolution()
			.Where(s => s.Id == id)
			// Skills
			.Include(u => u.Skills)
			.ThenInclude(s => s.LocalizedName)
			.Include(u => u.Skills)
			.ThenInclude(s => s.Talents)
			.ThenInclude(t => t.LocalizedName)
			.Include(u => u.Skills)
			.ThenInclude(s => s.Talents)
			.ThenInclude(t => t.LocalizedDescription)
			// Crafting Tables
			.Include(u => u.CraftingTables)
			.ThenInclude(ct => ct.PluginModules)
			.Include(u => u.CraftingTables)
			.ThenInclude(s => s.LocalizedName)
			// Plugin Modules
			.Include(u => u.PluginModules)
			.ThenInclude(s => s.LocalizedName)
			.Include(u => u.PluginModules)
			.ThenInclude(s => s.Skill)
			// Recipe
			.Include(u => u.Recipes)
			.ThenInclude(c => c.Elements)
			.ThenInclude(e => e.Quantity)
			.ThenInclude(dv => dv.Modifiers)
			.Include(u => u.Recipes)
			.ThenInclude(s => s.LocalizedName)
			.Include(u => u.Recipes)
			.ThenInclude(s => s.CraftMinutes)
			.ThenInclude(dv => dv.Modifiers)
			.Include(u => u.Recipes)
			.ThenInclude(s => s.Labor)
			.ThenInclude(dv => dv.Modifiers)
			// ItemOrTag
			.Include(u => u.ItemOrTags)
			.ThenInclude(s => s.AssociatedItems)
			.Include(u => u.ItemOrTags)
			.ThenInclude(s => s.AssociatedTags)
			.Include(u => u.ItemOrTags)
			.ThenInclude(s => s.LocalizedName)
			.FirstAsync();
	}

	public async Task<Server?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<Server?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.Servers
			.Include(s => s.UserServers)
			.ThenInclude(us => us.User)
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public async Task<Server?> GetByEcoServerIdAsync(string ecoServerId)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByEcoServerIdAsync(ecoServerId, context);
	}

	public async Task<Server?> GetByEcoServerIdAsync(string ecoServerId, EcoCraftDbContext context)
	{
		return await context.Servers
			.FirstOrDefaultAsync(s => s.EcoServerId == ecoServerId);
	}

	public async Task<Server?> GetByApiKeyAsync(Guid apiKey)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByApiKeyAsync(apiKey, context);
	}

	public async Task<Server?> GetByApiKeyAsync(Guid apiKey, EcoCraftDbContext context)
	{
		return await context.Servers
			.FirstOrDefaultAsync(s => s.ApiKey == apiKey);
	}

    public async Task<Server?> GetByJoinCodeAsync(string joinCode)
    {
		await using var context = await factory.CreateDbContextAsync();
		return await GetByJoinCodeAsync(joinCode, context);
    }

    public async Task<Server?> GetByJoinCodeAsync(string joinCode, EcoCraftDbContext context)
    {
        return await context.Servers
	        .FirstOrDefaultAsync(s => s.JoinCode == joinCode);
    }

    public async Task<RegisterServerResultInfo> RegisterServerAsync(string joinCode, string ecoServerId)
    {
	    await using var context = await factory.CreateDbContextAsync();

	    // 1. On récupère le serveur Eco-Gnome ciblé
	    var server = await context.Servers
		    .FirstOrDefaultAsync(s => s.JoinCode == joinCode);

	    if (server is null)
	    {
		    return new RegisterServerResultInfo
		    {
			    Result = RegisterServerResult.EcoGnomeServerNotFound
		    };
	    }

	    // 2. On regarde si l'ecoServerId est déjà utilisé ailleurs
	    var alreadyRegisteredServer = await context.Servers
		    .FirstOrDefaultAsync(s => s.EcoServerId == ecoServerId);

	    if (alreadyRegisteredServer is not null && alreadyRegisteredServer.Id != server.Id)
	    {
		    return new RegisterServerResultInfo
		    {
			    Result = RegisterServerResult.EcoServerAlreadyRegisteredWithOtherEcoServer,
			    OtherServerName = alreadyRegisteredServer.Name
		    };
	    }

	    // 3. On vérifie si ce serveur Eco-Gnome est déjà lié à un autre Eco server
	    if (server.EcoServerId is not null && server.EcoServerId != ecoServerId)
	    {
		    return new RegisterServerResultInfo
		    {
			    Result = RegisterServerResult.EcoGnomeServerAlreadyLinkedToOtherEcoServer
		    };
	    }

	    // 4. On fait l'association
	    server.EcoServerId = ecoServerId;

	    await context.SaveChangesAsync();

	    return new RegisterServerResultInfo
	    {
		    Result = RegisterServerResult.Success
	    };
    }

    private Server CloneForDb(Server server)
    {
	    return new Server
	    {
		    Id = server.Id,
		    Name = server.Name,
		    EcoServerId = server.EcoServerId,
		    IsDefault = server.IsDefault,
		    HasVideoUploader = server.HasVideoUploader,
		    IsCalorieCostLocked = server.IsCalorieCostLocked,
		    LockedCalorieCost = server.LockedCalorieCost,
		    CalorieCostMin = server.CalorieCostMin,
		    CalorieCostDefault = server.CalorieCostDefault,
		    CalorieCostMax = server.CalorieCostMax,
		    CreationDateTime = server.CreationDateTime,
		    LastDataUploadTime = server.LastDataUploadTime,
		    JoinCode = server.JoinCode,
		    ApiKey = server.ApiKey,
	    };
    }

    public void Create(EcoCraftDbContext context, Server server)
    {
	    context.Add(CloneForDb(server));
    }

    public void UpdateAll(EcoCraftDbContext context, Server server)
    {
	    context.Attach(CloneForDb(server)).State = EntityState.Modified;
    }

    public void UpdateEcoServerId(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server { Id = server.Id, EcoServerId = server.EcoServerId };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.EcoServerId).IsModified = true;
    }

    public void UpdateApiKey(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server { Id = server.Id, ApiKey = server.ApiKey };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.ApiKey).IsModified = true;
    }

    public void UpdateName(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server { Id = server.Id, Name = server.Name };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.Name).IsModified = true;
    }

    public void UpdateJoinCode(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server { Id = server.Id, JoinCode = server.JoinCode };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.JoinCode).IsModified = true;
    }

    public void UpdateHasVideoUploader(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server { Id = server.Id, HasVideoUploader = server.HasVideoUploader };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.HasVideoUploader).IsModified = true;
    }

    public void UpdateCalorieCostPolicy(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server
	    {
		    Id = server.Id,
		    IsCalorieCostLocked = server.IsCalorieCostLocked,
		    LockedCalorieCost = server.LockedCalorieCost,
		    CalorieCostMin = server.CalorieCostMin,
		    CalorieCostDefault = server.CalorieCostDefault,
		    CalorieCostMax = server.CalorieCostMax
	    };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.IsCalorieCostLocked).IsModified = true;
	    entry.Property(x => x.LockedCalorieCost).IsModified = true;
	    entry.Property(x => x.CalorieCostMin).IsModified = true;
	    entry.Property(x => x.CalorieCostDefault).IsModified = true;
	    entry.Property(x => x.CalorieCostMax).IsModified = true;
    }

    public void UpdateIsDefault(EcoCraftDbContext context, Server server)
    {
	    var stub = new Server { Id = server.Id, IsDefault = server.IsDefault };
	    var entry = context.Entry(stub);
	    entry.State = EntityState.Unchanged;
	    entry.Property(x => x.IsDefault).IsModified = true;
    }

    public void Destroy(EcoCraftDbContext context, Server server)
    {
	    var entity = new Server { Id = server.Id };
	    context.Entry(entity).State = EntityState.Deleted;
    }
}
