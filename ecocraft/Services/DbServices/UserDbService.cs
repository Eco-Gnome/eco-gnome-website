using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public enum RegisterUserResult
{
	Success,
	EcoServerNotFound,
	InvalidUserSecretId,
	EcoGnomeUserNotFound,
	UserNotInServer,
	UserAlreadyAssociatedToAnotherEcoUser
}

public class UserDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<User>
{
	public async Task<List<User>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Users
			.Include(u => u.UserServers)
			.ThenInclude(us => us.Server)
			.OrderByDescending(u => u.CreationDateTime)
			.ToListAsync();
	}

	public async Task<User?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Users
			.Include(u => u.UserServers)
			.ThenInclude(us => us.Server)
			.FirstOrDefaultAsync(u => u.Id == id);
	}

	public async Task<User?> GetByIdAndSecretAsync(Guid id, Guid secretId, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Users
			.Include(u => u.UserServers)
			.ThenInclude(us => us.Server)
			.Include(u => u.UserServers)
			.ThenInclude(us => us.DataContexts)
			.FirstOrDefaultAsync(u => u.Id == id && u.SecretId == secretId);
	}

	public async Task<List<UserServer>> GetUserServerByEcoIdsAsync(string ecoUserId, string ecoServerId)
	{
		await using var context = await factory.CreateDbContextAsync();

		return await context.UserServers
			.Include(us => us.Server)
			.Include(us => us.DataContexts)
			.Include(us => us.User)
			.Where(us => us.EcoUserId == ecoUserId && us.Server.EcoServerId == ecoServerId)
			.OrderByDescending(us => us.User.CreationDateTime)
			.ToListAsync();
	}

	public async Task<User?> SearchByIdAndSecretAsync(Guid id, Guid secretId)
	{
		await using var context = await factory.CreateDbContextAsync();

		return await context.Users
			.FirstOrDefaultAsync(u => u.Id == id && u.SecretId == secretId);
	}

	public async Task<RegisterUserResult> RegisterUserAsync(string ecoServerId, string userSecretId, string ecoUserId, string serverPseudo)
	{
		await using var context = await factory.CreateDbContextAsync();

		if (!Guid.TryParse(userSecretId, out var userSecretGuid))
		{
			return RegisterUserResult.InvalidUserSecretId;
		}

		var server = await context.Servers
			.FirstOrDefaultAsync(s => s.EcoServerId == ecoServerId);

		if (server is null)
		{
			return RegisterUserResult.EcoServerNotFound;
		}

		var user = await context.Users
			.Include(u => u.UserServers)
			.ThenInclude(us => us.Server)
			.FirstOrDefaultAsync(u => u.SecretId == userSecretGuid);

		if (user is null)
		{
			return RegisterUserResult.EcoGnomeUserNotFound;
		}

		var userServer = user.UserServers
			.FirstOrDefault(us => us.Server.EcoServerId == ecoServerId);

		if (userServer is null)
		{
			return RegisterUserResult.UserNotInServer;
		}

		if (userServer.EcoUserId is not null && userServer.EcoUserId != ecoUserId)
		{
			return RegisterUserResult.UserAlreadyAssociatedToAnotherEcoUser;
		}

		userServer.EcoUserId = ecoUserId;
		userServer.Pseudo = serverPseudo;

		await context.SaveChangesAsync();

		return RegisterUserResult.Success;
	}

	public async Task<int> CountUsers()
	{
		await using var context = await factory.CreateDbContextAsync();

		return await context.Users.CountAsync();
	}

	private User CloneForDb(User user)
	{
		return new User
		{
			Id = user.Id,
			Pseudo = user.Pseudo,
			CreationDateTime = user.CreationDateTime,
			SecretId = user.SecretId,
			SuperAdmin = user.SuperAdmin,
			CanUploadMod = user.CanUploadMod,
			ShowHelp = user.ShowHelp,
		};
	}

	public void Create(EcoCraftDbContext context, User user)
	{
		context.Add(CloneForDb(user));
	}

	public void UpdateAll(EcoCraftDbContext context, User user)
	{
		context.Attach(CloneForDb(user)).State = EntityState.Modified;
	}

	public void UpdateCanUploadMod(EcoCraftDbContext context, User user)
	{
		var entry = context.Attach(user);
		entry.Property(x => x.CanUploadMod).IsModified = true;
	}

	public void UpdateSuperAdmin(EcoCraftDbContext context, User user)
	{
		var entry = context.Attach(user);
		entry.Property(x => x.SuperAdmin).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, User user)
	{
		var entity = new User { Id = user.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
