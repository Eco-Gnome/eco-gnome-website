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

	public Task<UserElement?> GetByIdAsync(Guid id)
	{
		return context.UserElements
			.FirstOrDefaultAsync(ue => ue.Id == id);
	}

	public UserElement Add(UserElement userElement)
	{
		context.UserElements.Add(userElement);
		userElement.Element.CurrentUserElement = userElement;

		return userElement;
	}

	public void Update(UserElement userElement)
	{
		context.UserElements.Update(userElement);
	}

	public void Delete(UserElement userElement)
	{
		userElement.Element.CurrentUserElement = null;

		context.UserElements.Remove(userElement);
	}
}
