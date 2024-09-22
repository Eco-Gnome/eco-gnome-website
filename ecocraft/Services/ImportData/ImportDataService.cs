using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ecocraft.Services.ImportData
{


	public class ImportDataDto
	{
		public List<RecipeDto> recipes { get; set; }
		public List<ItemTagAssocDto> itemTagAssoc { get; set; }
		public List<PluginModuleDto> pluginModules { get; set; }
		public List<CraftingTableDto> craftingTables { get; set; }
		public List<SkillDto> skills { get; set; }
	}

	public class RecipeDto
	{
		public string Name { get; set; }
		public string FamilyName { get; set; }
		public double CraftMinutes { get; set; }
		public string RequiredSkill { get; set; }  // Change enum to string
		public long RequiredSkillLevel { get; set; }
		public bool IsBlueprint { get; set; }
		public bool IsDefault { get; set; }
		public double Labor { get; set; }
		public string CraftingTable { get; set; }
		public List<IngredientDto> Ingredients { get; set; }
		public List<IngredientDto> Products { get; set; }
	}

	public class IngredientDto
	{
		public string ItemOrTag { get; set; }
		public double Quantity { get; set; }
		public bool IsDynamic { get; set; }
		public string Skill { get; set; }
		public bool LavishTalent { get; set; }
	}

	public class CraftingTableDto
	{
		public string Name { get; set; }
		public List<string> CraftingTablePluginModules { get; set; }
	}

	public class ItemTagAssocDto
	{
		public string Tag { get; set; }
		public List<string> Types { get; set; }
	}

	public class PluginModuleDto
	{
		public string Name { get; set; }
		public double Percent { get; set; }
	}

	public class SkillDto
	{
		public string Name { get; set; }  // Change enum to string
	}


}
