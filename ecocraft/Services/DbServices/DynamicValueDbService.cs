using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class DynamicValueDbService(EcoCraftDbContext context)
{
	public Task<List<DynamicValue>> GetAllAsync()
	{
		return context.DynamicValues
			.ToListAsync();
	}

	public Task<DynamicValue?> GetByIdAsync(Guid id)
	{
		return context.DynamicValues
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public DynamicValue Add(DynamicValue dynamicValue)
	{
		context.DynamicValues.Add(dynamicValue);

		return dynamicValue;
	}

	public void Update(DynamicValue dynamicValue)
	{
		context.DynamicValues.Update(dynamicValue);
	}

	public void Delete(DynamicValue dynamicValue)
	{
		context.DynamicValues.Remove(dynamicValue);
	}
}
