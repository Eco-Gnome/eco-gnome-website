using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class DynamicValueDbService(IDbContextFactory<EcoCraftDbContext> factory)
{
	public async Task<List<DynamicValue>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.DynamicValues
			.ToListAsync();
	}

	public async Task<DynamicValue?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.DynamicValues
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	private DynamicValue CloneForDb(DynamicValue dynamicValue)
	{
		return new DynamicValue
		{
			Id = dynamicValue.Id,
			BaseValue = dynamicValue.BaseValue,
			ServerId = dynamicValue.Server.Id,
		};
	}

	public void Create(EcoCraftDbContext context, DynamicValue dynamicValue)
	{
		context.Add(CloneForDb(dynamicValue));
	}

	public void UpdateAll(EcoCraftDbContext context, DynamicValue dynamicValue)
	{
		context.Attach(CloneForDb(dynamicValue)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, DynamicValue dynamicValue)
	{
		var entity = new DynamicValue { Id = dynamicValue.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
