﻿using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserPriceService : IGenericService<UserPrice>
	{
		private readonly EcoCraftDbContext _context;

		public UserPriceService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<UserPrice>> GetAllAsync()
		{
			return await _context.UserPrices.Include(up => up.User)
											.Include(up => up.ItemOrTag)
											.Include(up => up.Server)
											.ToListAsync();
		}

		public async Task<UserPrice> GetByIdAsync(Guid id)
		{
			return await _context.UserPrices.Include(up => up.User)
											.Include(up => up.ItemOrTag)
											.Include(up => up.Server)
											.FirstOrDefaultAsync(up => up.Id == id);
		}

		public async Task AddAsync(UserPrice userPrice)
		{
			await _context.UserPrices.AddAsync(userPrice);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserPrice userPrice)
		{
			_context.UserPrices.Update(userPrice);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userPrice = await GetByIdAsync(id);
			if (userPrice != null)
			{
				_context.UserPrices.Remove(userPrice);
				await _context.SaveChangesAsync();
			}
		}
	}

}