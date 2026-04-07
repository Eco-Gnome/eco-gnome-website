using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ModUploadHistoryDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<ModUploadHistory>
{
	public async Task<List<ModUploadHistory>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<ModUploadHistory>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.ModUploadHistories
			.Include(muh => muh.User)
			.Include(muh => muh.Server)
			.OrderByDescending(muh => muh.UploadDateTime)
			.ToListAsync();
	}

	public async Task<ModUploadHistory?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<ModUploadHistory?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.ModUploadHistories
			.Include(muh => muh.User)
			.FirstOrDefaultAsync(muh => muh.Id == id);
	}

	private ModUploadHistory CloneForDb(ModUploadHistory modUploadHistory)
	{
		return new ModUploadHistory
		{
			Id = modUploadHistory.Id,
			FileName = modUploadHistory.FileName,
			FileHash = modUploadHistory.FileHash,
			IconsCount = modUploadHistory.IconsCount,
			UploadDateTime = modUploadHistory.UploadDateTime,
			UserId = modUploadHistory.User.Id,
			ServerId = modUploadHistory.Server?.Id,
		};
	}

	public void Create(EcoCraftDbContext context, ModUploadHistory modUploadHistory)
	{
		context.Add(CloneForDb(modUploadHistory));
	}

	public void UpdateAll(EcoCraftDbContext context, ModUploadHistory modUploadHistory)
	{
		context.Attach(CloneForDb(modUploadHistory)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, ModUploadHistory modUploadHistory)
	{
		var entity = new ModUploadHistory { Id = modUploadHistory.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
