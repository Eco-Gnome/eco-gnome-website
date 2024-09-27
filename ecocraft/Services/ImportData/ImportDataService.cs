using System.Text.Json;
using ecocraft.Models;

namespace ecocraft.Services.ImportData
{
	public class ImportDataService(
		EcoDataService ecoDataService,
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
					await ImportSkills(server, importedData.skills);
					await ImportPluginModules(server, importedData.pluginModules);
					await ImportCraftingTables(server, importedData.craftingTables);
					await ImportRecipes(server, importedData.recipes, importedData.itemTagAssoc);
					await ImportItemTagAssoc(importedData.itemTagAssoc);
				}
			}
			catch (JsonException ex)
			{
				Console.WriteLine($"Erreur lors de la désérialisation JSON: {ex.Message}");
			}
		}

		private async Task ImportSkills(Server server, List<SkillDto> skillDtos)
		{
			var namesToCheck = skillDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingNames = ecoDataService.Skills
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name);

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

					ecoDataService.Skills.Add(newSkill);
					await skillDbService.AddAsync(newSkill);
				}
			}
		}

		private async Task ImportPluginModules(Server server, List<PluginModuleDto> pluginModuleDtos)
		{
			var namesToCheck = pluginModuleDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingNames = ecoDataService.PluginModules
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name);

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

					ecoDataService.PluginModules.Add(newPluginModule);
					await pluginModuleDbService.AddAsync(newPluginModule);
				}
			}

			var existingUserSkill =
				ecoDataService.PluginModules.FirstOrDefault(pm => pm.Name.Contains("NoUpgrade") && pm.Server.Id == server.Id);
			if (existingUserSkill == null)
				await pluginModuleDbService.AddAsync(new PluginModule
					{ Name = "NoUpgrade", Percent = 0, Server = server });

		}

		private async Task ImportCraftingTables(Server server, List<CraftingTableDto> craftingTableDtos)
		{
			var namesToCheck = craftingTableDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingNames = ecoDataService.CraftingTables
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name);

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
							.Select(ctpm => ecoDataService.PluginModules.FirstOrDefault(pm => pm.Name == ctpm)).ToList()
					};

					ecoDataService.CraftingTables.Add(newCraftingTable);
					await craftingTableDbService.AddAsync(newCraftingTable);
				}
			}
		}

		private async Task ImportRecipes(Server server, List<RecipeDto> recipeDtos, List<ItemTagAssocDto> itemTagAssoc)
		{
			var namesToCheck = recipeDtos.Select(dto => dto.Name).ToList();

			// Récupère les noms qui existent déjà dans la base
			var existingRecipeNames = ecoDataService.Recipes
				.Where(ct => namesToCheck.Contains(ct.Name))
				.Select(ct => ct.Name);

			// Récupère les itemsOrTag qui existent déjà dans la base
			var existingItemOrTags = ecoDataService.ItemOrTags
				.Select(ct => ct.Name);

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
						Skill = ecoDataService.Skills.FirstOrDefault(s => s.Name == recipeDto.RequiredSkill),
						SkillLevel = recipeDto.RequiredSkillLevel,
						IsBlueprint = recipeDto.IsBlueprint,
						IsDefault = recipeDto.IsDefault,
						Labor = recipeDto.Labor,
						CraftingTable = ecoDataService.CraftingTables.FirstOrDefault(c => c.Name == recipeDto.CraftingTable),
						Server = server,
					};

					ecoDataService.Recipes.Add(newRecipe);
					await recipeDbService.AddAsync(newRecipe);

					// Si l'item or tag n'existe pas actuellement, on le crée, avant de créer l'element
					foreach (var recipeItem in recipeDto.Ingredients.Concat(recipeDto.Products))
					{
						if (!existingItemOrTags.Contains(recipeItem.ItemOrTag))
						{
							var itemOrTag = new ItemOrTag
							{
								Name = recipeItem.ItemOrTag,
								IsTag = itemTagAssoc.Select(t => t.Tag).Contains(recipeItem.ItemOrTag),
								Server = server,
							};

							ecoDataService.ItemOrTags.Add(itemOrTag);
							await itemOrTagDbService.AddAsync(itemOrTag);
						}
					}

					foreach (var recipeDtoIngredient in recipeDto.Ingredients)
					{
						recipeDtoIngredient.Quantity *= -1;
					}

					foreach (var element in recipeDto.Ingredients.Concat(recipeDto.Products))
					{
						var itemOrTag = await itemOrTagDbService.GetByNameAsync(element.ItemOrTag);
						var skill = ecoDataService.Skills.FirstOrDefault(s => s.Name == element.Skill);

						var ing = new Element
						{
							Recipe = newRecipe,
							ItemOrTag = itemOrTag,
							Quantity = element.Quantity,
							IsDynamic = element.IsDynamic,
							Skill = skill,
							LavishTalent = element.LavishTalent,
						};

						await elementDbService.AddAsync(ing);
					}
				}
			}
		}

		private async Task ImportItemTagAssoc(List<ItemTagAssocDto> itemTagAssoc)
		{
			foreach (var itemTag in itemTagAssoc)
			{
				var tag = ecoDataService.ItemOrTags.FirstOrDefault(i => i.Name == itemTag.Tag);

				if (tag is not null)
				{
					foreach (var type in itemTag.Types)
					{
						var item = ecoDataService.ItemOrTags.FirstOrDefault(i => i.Name == type);

						if (item is not null)
						{
							tag.AssociatedItemOrTags.Add(item);
						}
					}

					await itemOrTagDbService.UpdateAsync(tag);
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
