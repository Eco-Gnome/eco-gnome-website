using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ModifierDbService(IDbContextFactory<EcoCraftDbContext> factory)
{
	public async Task<List<Modifier>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Modifiers
			.ToListAsync();
	}

	public async Task<Modifier?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Modifiers
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	private Modifier CloneForDb(Modifier modifier)
	{
		return new Modifier
		{
			Id = modifier.Id,
			DynamicType = modifier.DynamicType,
			ValueType = modifier.ValueType,
			DynamicValueId = modifier.DynamicValue.Id,
			SkillId = modifier.Skill?.Id,
			TalentId = modifier.Talent?.Id,
		};
	}

	public void Create(EcoCraftDbContext context, Modifier modifier)
	{
		context.Add(CloneForDb(modifier));
	}

	public void UpdateAll(EcoCraftDbContext context, Modifier modifier)
	{
		context.Attach(CloneForDb(modifier)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, Modifier modifier)
	{
		var entity = new Modifier { Id = modifier.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
