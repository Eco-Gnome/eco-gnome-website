using ecocraft.Models;

namespace ecocraft.Services;

// POCO edit-models used by the /data-editor dialogs. They are intentionally decoupled from the
// EF entities: dialogs bind to these, then ServerDataEditorService maps them onto tracked entities.
//
// Naming: every editable entity carries TWO distinct names.
//   - Name              : the TECHNICAL name. It is the import match key (ImportDataService matches
//                         entities by Name and deletes any whose Name is absent from the import), so
//                         it is edited explicitly and never derived from the localized name.
//   - LocalizedNameEnUs : the displayed name in English (the en_US fallback column).
//   - LocalizedNameCurrent : the displayed name in the current UI language (only meaningful, and only
//                         shown/applied, when the UI language is not en_US).

public class ItemEditModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string LocalizedNameEnUs { get; set; } = "";
    public string LocalizedNameCurrent { get; set; } = "";
    public bool IsTag { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    // Two ends of the same Item<->Tag M:M (join table ItemTagAssoc). Only the end matching IsTag is
    // populated/applied: an item edits the tags it belongs to (AssociatedTagIds), a tag edits the
    // items it groups (AssociatedItemIds).
    public List<Guid> AssociatedTagIds { get; set; } = [];
    public List<Guid> AssociatedItemIds { get; set; } = [];
}

// Reused for both ingredients and products of a recipe. Quantity is always stored as a positive
// number here; the service applies the sign (negative for ingredients, positive for products).
public class RecipeIngredientModel
{
    public Guid ItemOrTagId { get; set; }
    public decimal Quantity { get; set; }
}

public class RecipeEditModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string LocalizedNameEnUs { get; set; } = "";
    public string LocalizedNameCurrent { get; set; } = "";
    public Guid? SkillId { get; set; }
    public long SkillLevel { get; set; }
    public Guid CraftingTableId { get; set; }
    public decimal Labor { get; set; }
    public decimal CraftMinutes { get; set; }
    public List<RecipeIngredientModel> Ingredients { get; set; } = [];
    public List<RecipeIngredientModel> Products { get; set; } = [];
}

public class SkillEditModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string LocalizedNameEnUs { get; set; } = "";
    public string LocalizedNameCurrent { get; set; } = "";
    public string? Profession { get; set; }
    public int MaxLevel { get; set; }
    public decimal[] LaborReducePercent { get; set; } = [];
}

public class CraftingTableEditModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string LocalizedNameEnUs { get; set; } = "";
    public string LocalizedNameCurrent { get; set; } = "";
    public List<Guid> PluginModuleIds { get; set; } = [];
    public List<Guid> ModuleSlotIds { get; set; } = [];
}

// One bonus row of a plugin module (v4 slot+bonus system). The skill/tag filters are edited as
// comma-separated technical names, mapped to string[] by the service.
public class BonusEditModel
{
    public TalentBonusAction Action { get; set; } = TalentBonusAction.ResourceCost;
    public TalentBonusEffectType EffectType { get; set; } = TalentBonusEffectType.AdditivePercent;
    public decimal Value { get; set; }
    public decimal? Cap { get; set; }
    public decimal? Chance { get; set; }
    // Not editable in the dialog, but carried through so saving a module does not wipe the
    // per-level multipliers of an imported TieredMultiplicative bonus.
    public decimal[]? Levels { get; set; }
    public string SkillTypes { get; set; } = "";
    public string ExcludedSkillTypes { get; set; } = "";
    public string ItemTags { get; set; } = "";
}

public class PluginModuleEditModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string LocalizedNameEnUs { get; set; } = "";
    public string LocalizedNameCurrent { get; set; } = "";
    public Guid? ModuleSlotId { get; set; }
    public decimal? MaterialTierBump { get; set; }
    public List<BonusEditModel> Bonuses { get; set; } = [];
}
