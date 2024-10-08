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

	public Task<List<UserElement>> GetByUserServerAsync(UserServer userServer)
	{
		return context.UserElements
			.Where(s => s.UserServerId == userServer.Id)
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