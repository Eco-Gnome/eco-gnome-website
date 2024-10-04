using System.Text.Json;
using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services.ImportData
{
	public class ImportDataService(
		EcoCraftDbContext dbContext,
		ServerDataService serverDataService,
		SkillDbService skillDbService, 
		PluginModuleDbService pluginModuleDbService,
		CraftingTableDbService craftingTableDbService,
		ItemOrTagDbService itemOrTagDbService,
		ElementDbService elementDbService,
		RecipeDbService recipeDbService)
	{
		// TODO: Handle an update of server data
		public async Task ImportServerData(string jsonContent, Server server)
		{
			try {
				ImportDataDto? importedData = JsonSerializer.Deserialize<ImportDataDto>(jsonContent);

				if (importedData is not null)
				{
					ImportSkills(server, importedData.skills);
					ImportPluginModules(server, importedData.pluginModules);
					ImportCraftingTables(server, importedData.craftingTables);
					await ImportRecipes(server, importedData.recipes, importedData.itemTagAssoc);
					ImportItemTagAssoc(importedData.itemTagAssoc);

					await dbContext.SaveChangesAsync();
				}
			}
			catch (JsonException ex)
			{
				Console.WriteLine($"Erreur lors de la désérialisation JSON: {ex.Message}");
			}
		}

		private void ImportSkills(Server server, List<SkillDto> skillDtos)
		{
			var namesToCheck = skillDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingNames = serverDataService.Skills
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name)
				.ToList();

			// Ajouter uniquement les objets qui n'existent pas
			foreach (var dto in skillDtos)
			{
				if (!existingNames.Contains(dto.Name))
				{
					var newSkill = new Skill
					{
						Name = dto.Name,
						Server = server,
					};

					serverDataService.Skills.Add(newSkill);
					skillDbService.Add(newSkill);
				}
			}
		}

		private void ImportPluginModules(Server server, List<PluginModuleDto> pluginModuleDtos)
		{
			var namesToCheck = pluginModuleDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingNames = serverDataService.PluginModules
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name)
				.ToList();

			// Ajouter uniquement les objets qui n'existent pas
			foreach (var dto in pluginModuleDtos)
			{
				if (!existingNames.Contains(dto.Name))
				{
					var newPluginModule = new PluginModule
					{
						Name = dto.Name,
						Percent = dto.Percent,
						Server = server,
					};

					serverDataService.PluginModules.Add(newPluginModule);
					pluginModuleDbService.Add(newPluginModule);
				}
			}

			var existingUserSkill =
				serverDataService.PluginModules.FirstOrDefault(pm => pm.Name.Contains("NoUpgrade") && pm.Server.Id == server.Id);
			if (existingUserSkill == null)
				pluginModuleDbService.Add(new PluginModule
					{ Name = "NoUpgrade", Percent = 0, Server = server });

		}

		private void ImportCraftingTables(Server server, List<CraftingTableDto> craftingTableDtos)
		{
			var namesToCheck = craftingTableDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingNames = serverDataService.CraftingTables
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name)
				.ToList();

			// Ajouter uniquement les objets qui n'existent pas
			foreach (var dto in craftingTableDtos)
			{
				if (!existingNames.Contains(dto.Name))
				{
					var newCraftingTable = new CraftingTable
					{
						Name = dto.Name,
						Server = server,
						PluginModules = dto.CraftingTablePluginModules
							.Select(ctpm => serverDataService.PluginModules.First(pm => pm.Name == ctpm)).ToList()
					};

					serverDataService.CraftingTables.Add(newCraftingTable);
					craftingTableDbService.Add(newCraftingTable);
				}
			}
		}

		private async Task ImportRecipes(Server server, List<RecipeDto> recipeDtos, List<ItemTagAssocDto> itemTagAssoc)
		{
			var namesToCheck = recipeDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingRecipeNames = serverDataService.Recipes
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name)
				.ToList();

			// Récupère les itemsOrTag qui existent déjà dans la base
			var existingItemOrTags = serverDataService.ItemOrTags
				.Select(ct => ct.Name)
				.ToList();

			// Ajouter uniquement les objets qui n'existent pas
			foreach (var recipeDto in recipeDtos)
			{
				if (!existingRecipeNames.Contains(recipeDto.Name))
				{
					var newRecipe = new Recipe
					{
						Name = recipeDto.Name,
						FamilyName = recipeDto.FamilyName,
						CraftMinutes = recipeDto.CraftMinutes,
						Skill = serverDataService.Skills.FirstOrDefault(s => s.Name == recipeDto.RequiredSkill),
						SkillLevel = recipeDto.RequiredSkillLevel,
						IsBlueprint = recipeDto.IsBlueprint,
						IsDefault = recipeDto.IsDefault,
						Labor = recipeDto.Labor,
						CraftingTable = serverDataService.CraftingTables.First(c => c.Name == recipeDto.CraftingTable),
						Server = server,
					};

					serverDataService.Recipes.Add(newRecipe);
					recipeDbService.Add(newRecipe);

					// Si l'item or tag n'existe pas actuellement, on le crée, avant de créer l'element
					foreach (var recipeItem in recipeDto.Ingredients.Concat(recipeDto.Products))
					{
						if (existingItemOrTags.Contains(recipeItem.ItemOrTag)) continue;
						
						var itemOrTag = new ItemOrTag
						{
							Name = recipeItem.ItemOrTag,
							IsTag = itemTagAssoc.Select(t => t.Tag).Contains(recipeItem.ItemOrTag),
							Server = server,
						};

						serverDataService.ItemOrTags.Add(itemOrTag);
						itemOrTagDbService.Add(itemOrTag);
					}

					foreach (var recipeDtoIngredient in recipeDto.Ingredients)
					{
						recipeDtoIngredient.Quantity *= -1;
					}

					foreach (var element in recipeDto.Ingredients.Concat(recipeDto.Products))
					{
						var itemOrTag = itemOrTagDbService.GetByName(element.ItemOrTag)!;
						var skill = serverDataService.Skills.First(s => s.Name == element.Skill);

						var ing = new Element
						{
							Recipe = newRecipe,
							ItemOrTag = itemOrTag,
							Quantity = element.Quantity,
							IsDynamic = element.IsDynamic,
							Skill = skill,
							LavishTalent = element.LavishTalent,
						};

						elementDbService.Add(ing);
					}
				}
			}
		}

		private void ImportItemTagAssoc(List<ItemTagAssocDto> itemTagAssoc)
		{
			foreach (var itemTag in itemTagAssoc)
			{
				var tag = serverDataService.ItemOrTags.FirstOrDefault(i => i.Name == itemTag.Tag);

				if (tag is not null)
				{
					foreach (var type in itemTag.Types)
					{
						var item = serverDataService.ItemOrTags.FirstOrDefault(i => i.Name == type);

						if (item is not null)
						{
							tag.AssociatedItemOrTags.Add(item);
						}
					}

					itemOrTagDbService.Update(tag);
				}
			}
		}
		
		private class ImportDataDto
		{
			public List<RecipeDto> recipes { get; set; }
			public List<ItemTagAssocDto> itemTagAssoc { get; set; }
			public List<PluginModuleDto> pluginModules { get; set; }
			public List<CraftingTableDto> craftingTables { get; set; }
			public List<SkillDto> skills { get; set; }
		}

		private class RecipeDto
		{
			public string Name { get; set; }
			public string FamilyName { get; set; }
			public float CraftMinutes { get; set; }
			public string RequiredSkill { get; set; }
			public long RequiredSkillLevel { get; set; }
			public bool IsBlueprint { get; set; }
			public bool IsDefault { get; set; }
			public float Labor { get; set; }
			public string CraftingTable { get; set; }
			public List<ElementDto> Ingredients { get; set; }
			public List<ElementDto> Products { get; set; }
		}

		private class ElementDto
		{
			public string ItemOrTag { get; set; }
			public float Quantity { get; set; }
			public bool IsDynamic { get; set; }
			public string Skill { get; set; }
			public bool LavishTalent { get; set; }
		}

		private class CraftingTableDto
		{
			public string Name { get; set; }
			public List<string> CraftingTablePluginModules { get; set; }
		}

		private class ItemTagAssocDto
		{
			public string Tag { get; set; }
			public List<string> Types { get; set; }
		}

		private class PluginModuleDto
		{
			public string Name { get; set; }
			public float Percent { get; set; }
		}

		private class SkillDto
		{
			public string Name { get; set; }
		}
	}

}
