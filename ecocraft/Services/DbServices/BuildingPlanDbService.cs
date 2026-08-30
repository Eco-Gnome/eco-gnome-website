using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class BuildingPlanDbService(IDbContextFactory<EcoCraftDbContext> factory)
{
	public async Task<List<BuildingPlan>> GetByUserServerAsync(UserServer userServer)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByUserServerAsync(userServer, context);
	}

	public async Task<List<BuildingPlan>> GetByUserServerAsync(UserServer userServer, EcoCraftDbContext context)
	{
		return await context.BuildingPlans
			.Where(bp => bp.UserServerId == userServer.Id)
			.OrderByDescending(bp => bp.UpdateDateTime)
			.ToListAsync();
	}

	// Liste sans le document (les plans peuvent peser plusieurs centaines de Ko chacun).
	public async Task<List<BuildingPlan>> GetSummariesByUserServerAsync(UserServer userServer)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await context.BuildingPlans
			.Where(bp => bp.UserServerId == userServer.Id)
			.OrderByDescending(bp => bp.UpdateDateTime)
			.Select(bp => new BuildingPlan
			{
				Id = bp.Id,
				UserServerId = bp.UserServerId,
				Name = bp.Name,
				SchemaVersion = bp.SchemaVersion,
				Document = "",
				CreationDateTime = bp.CreationDateTime,
				UpdateDateTime = bp.UpdateDateTime,
			})
			.ToListAsync();
	}

	public async Task<BuildingPlan?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<BuildingPlan?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.BuildingPlans
			.FirstOrDefaultAsync(bp => bp.Id == id);
	}

	private static BuildingPlan CloneForDb(BuildingPlan buildingPlan)
	{
		return new BuildingPlan
		{
			Id = buildingPlan.Id,
			UserServerId = buildingPlan.UserServerId,
			Name = buildingPlan.Name,
			SchemaVersion = buildingPlan.SchemaVersion,
			Document = buildingPlan.Document,
			CreationDateTime = buildingPlan.CreationDateTime,
			UpdateDateTime = buildingPlan.UpdateDateTime,
		};
	}

	public void Create(EcoCraftDbContext context, BuildingPlan buildingPlan)
	{
		context.Add(CloneForDb(buildingPlan));
	}

	public void UpdateContent(EcoCraftDbContext context, BuildingPlan buildingPlan)
	{
		var stub = new BuildingPlan
		{
			Id = buildingPlan.Id,
			Name = buildingPlan.Name,
			SchemaVersion = buildingPlan.SchemaVersion,
			Document = buildingPlan.Document,
			UpdateDateTime = buildingPlan.UpdateDateTime,
		};
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Name).IsModified = true;
		entry.Property(x => x.SchemaVersion).IsModified = true;
		entry.Property(x => x.Document).IsModified = true;
		entry.Property(x => x.UpdateDateTime).IsModified = true;
	}

	public void UpdateName(EcoCraftDbContext context, BuildingPlan buildingPlan)
	{
		var stub = new BuildingPlan { Id = buildingPlan.Id, Name = buildingPlan.Name, UpdateDateTime = buildingPlan.UpdateDateTime };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Name).IsModified = true;
		entry.Property(x => x.UpdateDateTime).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, BuildingPlan buildingPlan)
	{
		context.QueueDelete<BuildingPlan>(buildingPlan.Id);
	}
}
