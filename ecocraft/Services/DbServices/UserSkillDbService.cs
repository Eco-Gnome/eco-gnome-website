using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserSkillDbService(EcoCraftDbContext context) : IGenericUserDbService<UserSkill>
{
	public Task<List<UserSkill>> GetAllAsync()
	{
		return context.UserSkills
			.ToListAsync();
	}

	public Task<List<UserSkill>> GetByUserServerAsync(UserServer userServer)
	{
		return context.UserSkills
			.Where(s => s.UserServerId == userServer.Id)
			.ToListAsync();
	}

	public Task<UserSkill?> GetByIdAsync(Guid id)
	{
		return context.UserSkills
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	public UserSkill Add(UserSkill userSkill)
	{
		context.UserSkills.Add(userSkill);

		return userSkill;
	}

	public void Update(UserSkill userSkill)
	{
		context.UserSkills.Update(userSkill);
	}

	public void Delete(UserSkill userSkill)
	{
		context.UserSkills.Remove(userSkill);
	}
}