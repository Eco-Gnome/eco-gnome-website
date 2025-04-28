using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ModUploadHistoryDbService(EcoCraftDbContext context) : IGenericDbService<ModUploadHistory>
{
	public Task<List<ModUploadHistory>> GetAllAsync()
	{
		return context.ModUploadHistories
			.Include(muh => muh.User)
			.Include(muh => muh.Server)
			.OrderByDescending(muh => muh.UploadDateTime)
			.ToListAsync();
	}

	public Task<ModUploadHistory?> GetByIdAsync(Guid id)
	{
		return context.ModUploadHistories
			.Include(muh => muh.User)
			.FirstOrDefaultAsync(muh => muh.Id == id);
	}

	public ModUploadHistory Add(ModUploadHistory muh)
	{
		context.ModUploadHistories.Add(muh);

		return muh;
	}

	public void Update(ModUploadHistory muh)
	{
		context.ModUploadHistories.Update(muh);
	}

	public void Delete(ModUploadHistory muh)
	{
		context.ModUploadHistories.Remove(muh);
	}
}
