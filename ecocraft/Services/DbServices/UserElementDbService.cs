using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserElementDbService(EcoCraftDbContext context) : IGenericUserDbService<UserElement>
{
	public Task<List<UserElement>> GetAllAsync()
	{
		return context.UserElements
			.ToListAsync();
	}

	public Task<List<UserElement>> GetByDataContextAsync(DataContext dataContext)
	{
		return context.UserElements
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public Task<List<UserElement>> GetByDataContextForEcoApiAsync(DataContext dataContext)
	{
		return context.UserElements
			.Where(up => up.DataContextId == dataContext.Id)
			.Include(ue => ue.Element)
			.ThenInclude(e => e.Recipe)
			.Include(ue => ue.Element)
			.ThenInclude(e => e.Quantity)
			.ToListAsync();
	}

	public Task<UserElement?> GetByIdAsync(Guid id)
	{
		return context.UserElements
			.FirstOrDefaultAsync(ue => ue.Id == id);
	}

	public UserElement Add(UserElement userElement)
	{
		context.UserElements.Add(userElement);

		return userElement;
	}

	public void Update(UserElement userElement)
	{
		context.UserElements.Update(userElement);
	}

	public void Delete(UserElement userElement)
	{
		context.UserElements.Remove(userElement);
	}
}
