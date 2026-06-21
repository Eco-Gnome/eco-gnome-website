using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

// Manual CRUD on server data (Items/Tags, Recipes, Skills, CraftingTables, PluginModules) for the
// /data-editor admin page. There is NO IsManual flag: a later re-import overwrites everything, the
// UI just warns. Recipe editing is SIMPLIFIED (no Modifiers on DynamicValues).
//
// Every write goes through EcoCraftDbContext.ContextSaveAsync. We load the whole server graph with
// GetServerWithData + Attach so relations (Skill/CraftingTable/ItemOrTag/PluginModule) resolve by Id
// against tracked entities. Deletions never go through the tracker (cascade ordering races throw
// DbUpdateConcurrencyException); they use ctx.QueueDelete<T>(id) after detaching, mirroring the
// DetachAndQueueDelete pattern in ImportDataService.Crud.cs.
public class ServerDataEditorService(
    IDbContextFactory<EcoCraftDbContext> factory,
    ServerDbService serverDbService,
    LocalizationService localizationService)
{
    // ----- Item / Tag -----

    public async Task CreateOrUpdateItemAsync(Server server, ItemEditModel m)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            ItemOrTag item;

            if (IsNew(m.Id))
            {
                var localizedName = NewLocalizedField(ctx, sd, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                item = new ItemOrTag
                {
                    Name = m.Name,
                    LocalizedName = localizedName,
                    IsTag = m.IsTag,
                    MinPrice = m.MinPrice,
                    DefaultPrice = m.DefaultPrice,
                    MaxPrice = m.MaxPrice,
                    Server = sd,
                };
                sd.ItemOrTags.Add(item);
                ctx.ItemOrTags.Add(item);
            }
            else
            {
                item = sd.ItemOrTags.First(i => i.Id == m.Id);
                item.Name = m.Name;
                item.IsTag = m.IsTag;
                item.MinPrice = m.MinPrice;
                item.DefaultPrice = m.DefaultPrice;
                item.MaxPrice = m.MaxPrice;
                ApplyLocalizedName(item.LocalizedName, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                ctx.ItemOrTags.Update(item);
            }

            // Item<->Tag M:M (join table ItemTagAssoc). The dialog only fills the end matching IsTag:
            // an item edits the tags it belongs to (AssociatedTags), a tag edits the items it groups
            // (AssociatedItems). Clear the irrelevant end so a type flip leaves no stale link.
            if (m.IsTag)
            {
                var items = sd.ItemOrTags.Where(i => m.AssociatedItemIds.Contains(i.Id)).ToList();
                DiffManyToMany(item.AssociatedItems, items);
                item.AssociatedTags.Clear();
            }
            else
            {
                var tags = sd.ItemOrTags.Where(i => m.AssociatedTagIds.Contains(i.Id)).ToList();
                DiffManyToMany(item.AssociatedTags, tags);
                item.AssociatedItems.Clear();
            }
        });
    }

    // Reconcile a tracked M:M collection in place. Diffing (instead of reassigning) avoids join-row
    // DELETEs hitting already-removed rows, which throw DbUpdateConcurrencyException. Same rationale
    // as the CraftingTable.PluginModules diff above and RefreshTag in ImportDataService.Crud.cs.
    private static void DiffManyToMany(List<ItemOrTag> current, List<ItemOrTag> target)
    {
        var newSet = new HashSet<ItemOrTag>(target);
        var oldSet = new HashSet<ItemOrTag>(current);

        foreach (var existing in current.ToList())
        {
            if (!newSet.Contains(existing))
            {
                current.Remove(existing);
            }
        }

        foreach (var wanted in target)
        {
            if (!oldSet.Contains(wanted))
            {
                current.Add(wanted);
            }
        }
    }

    public async Task DeleteItemAsync(Server server, Guid itemId)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var item = sd.ItemOrTags.First(i => i.Id == itemId);
            DetachAndQueueDelete(ctx, item, item.Id);
        });
    }

    // ----- Recipe (simplified) -----

    public async Task CreateOrUpdateRecipeAsync(Server server, RecipeEditModel m)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var skill = m.SkillId is not null ? sd.Skills.First(s => s.Id == m.SkillId) : null;
            var craftingTable = sd.CraftingTables.First(c => c.Id == m.CraftingTableId);

            Recipe recipe;

            if (IsNew(m.Id))
            {
                var localizedName = NewLocalizedField(ctx, sd, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                var labor = NewDynamicValue(ctx, sd, m.Labor);
                var craftMinutes = NewDynamicValue(ctx, sd, m.CraftMinutes);

                recipe = new Recipe
                {
                    Name = m.Name,
                    LocalizedName = localizedName,
                    FamilyName = m.Name,
                    Skill = skill,
                    SkillLevel = m.SkillLevel,
                    IsBlueprint = false,
                    IsDefault = false,
                    Labor = labor,
                    CraftMinutes = craftMinutes,
                    CraftingTable = craftingTable,
                    Server = sd,
                };
                sd.Recipes.Add(recipe);
                ctx.Recipes.Add(recipe);
            }
            else
            {
                recipe = sd.Recipes.First(r => r.Id == m.Id);
                recipe.Name = m.Name;
                // Do NOT touch FamilyName here: it groups recipe variants and is set once at
                // creation/import. Overwriting it with m.Name on every edit silently renamed the
                // family (and broke family-based grouping/sorting) when only an unrelated field
                // like the crafting table changed.
                recipe.Skill = skill;
                recipe.SkillLevel = m.SkillLevel;
                recipe.CraftingTable = craftingTable;
                ApplyLocalizedName(recipe.LocalizedName, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                recipe.Labor.BaseValue = m.Labor;
                recipe.CraftMinutes.BaseValue = m.CraftMinutes;
                ctx.Recipes.Update(recipe);
                ctx.DynamicValues.Update(recipe.Labor);
                ctx.DynamicValues.Update(recipe.CraftMinutes);
            }

            // Diff the elements IN PLACE instead of wipe/recreate: re-match each desired line to an
            // existing Element by (item + role ingredient/product) and REFRESH it, keeping its Id stable.
            // Only genuinely new lines get a new Element, and only unmatched existing rows are deleted.
            // This mirrors ImportDataService.Import.cs ("do NOT churn element Ids"): a stable Element.Id is
            // what keeps every user's UserElement (prices, recipe selections) attached after an edit. A brute
            // recreate gives the elements new Ids, which Reconciliate then treats as orphans and silently
            // removes — dropping the recipe from the price calculator / shopping list. Ingredients carry a
            // negative quantity (sign = role), products positive.
            //
            // Index is assigned as TWO independent sequences (ingredients 0,1,2... and products 0,1...),
            // exactly like ImportDataService.Import.cs. This is a hard invariant: the rest of the app
            // identifies a recipe's main product as "the product at Index 0" (RecipeList.razor,
            // RecipeDialog.razor, PriceCalculator GetBestRelatedElement, ShoppingListComponent). Using a
            // single global counter put the product at Index 3, which threw on the .First(...) lookups and
            // made the recipe vanish from every list/dropdown.
            var desiredLines = m.Ingredients
                .Select(l => (l.ItemOrTagId, Quantity: -Math.Abs(l.Quantity), IsProduct: false))
                .Concat(m.Products.Select(l => (l.ItemOrTagId, Quantity: Math.Abs(l.Quantity), IsProduct: true)))
                .ToList();

            var existingElements = recipe.Elements.ToList();
            var matchedElements = new HashSet<Element>();
            var ingredientIndex = 0;
            var productIndex = 0;

            foreach (var line in desiredLines)
            {
                var itemOrTag = sd.ItemOrTags.First(i => i.Id == line.ItemOrTagId);
                var index = line.IsProduct ? productIndex++ : ingredientIndex++;

                // Null guards on nav props: an orphan/cross-server element can have a dangling FK that
                // identity-resolution can't populate. Unmatched here => handled by the orphan cleanup below.
                var match = existingElements.FirstOrDefault(e =>
                    !matchedElements.Contains(e)
                    && (object?)e.ItemOrTag is not null
                    && (object?)e.Quantity is not null
                    && e.ItemOrTag.Id == line.ItemOrTagId
                    && e.IsProduct() == line.IsProduct);

                if (match is null)
                {
                    var element = new Element
                    {
                        Recipe = recipe,
                        ItemOrTag = itemOrTag,
                        Index = index,
                        Quantity = NewDynamicValue(ctx, sd, line.Quantity),
                        DefaultIsReintegrated = false,
                        DefaultShare = 0,
                    };
                    recipe.Elements.Add(element);
                    ctx.Elements.Add(element);
                }
                else
                {
                    matchedElements.Add(match);
                    match.ItemOrTag = itemOrTag;
                    match.Index = index;
                    match.DefaultIsReintegrated = false;
                    match.Quantity.BaseValue = line.Quantity;
                    ctx.Elements.Update(match);
                    ctx.DynamicValues.Update(match.Quantity);
                }
            }

            // Lines removed by the edit: delete the now-orphan Elements (+ their Quantity DynamicValue),
            // routed through DetachAndQueueDelete to avoid the tracker racing the FK cascade at SaveChanges.
            foreach (var orphan in existingElements.Where(e => !matchedElements.Contains(e)))
            {
                if ((object?)orphan.Quantity is not null)
                {
                    DetachAndQueueDelete(ctx, orphan.Quantity, orphan.Quantity.Id);
                }
                recipe.Elements.Remove(orphan);
                DetachAndQueueDelete(ctx, orphan, orphan.Id);
            }

            // DefaultShare distribution across non-reintegrated products (copied from
            // ImportDataService.Import.cs): single product -> 1, otherwise first -> 0.8 and the
            // remaining (n-1) split 0.2 evenly.
            var productsToEdit = recipe.Elements
                .Where(e => e.IsProduct() && !e.DefaultIsReintegrated)
                .OrderBy(e => e.Index)
                .ToList();

            for (var i = 0; i < productsToEdit.Count; i++)
            {
                productsToEdit[i].DefaultShare = productsToEdit.Count > 1
                    ? i == 0 ? 0.8m : 0.2m / (productsToEdit.Count - 1)
                    : 1;
            }
        });
    }

    public async Task DeleteRecipeAsync(Server server, Guid recipeId)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var recipe = sd.Recipes.First(r => r.Id == recipeId);

            // Recipe cascade deletes its Elements but NOT the DynamicValues (Labor, CraftMinutes,
            // each Element.Quantity), so we queue those explicitly.
            DetachAndQueueDelete(ctx, recipe.Labor, recipe.Labor.Id);
            DetachAndQueueDelete(ctx, recipe.CraftMinutes, recipe.CraftMinutes.Id);

            foreach (var element in recipe.Elements.ToList())
            {
                DetachAndQueueDelete(ctx, element.Quantity, element.Quantity.Id);
            }

            DetachAndQueueDelete(ctx, recipe, recipe.Id);
        });
    }

    // ----- Skill -----

    public async Task CreateOrUpdateSkillAsync(Server server, SkillEditModel m)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            if (IsNew(m.Id))
            {
                var localizedName = NewLocalizedField(ctx, sd, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                var skill = new Skill
                {
                    Name = m.Name,
                    LocalizedName = localizedName,
                    Profession = m.Profession,
                    MaxLevel = m.MaxLevel,
                    LaborReducePercent = m.LaborReducePercent,
                    Server = sd,
                };
                sd.Skills.Add(skill);
                ctx.Skills.Add(skill);
            }
            else
            {
                var skill = sd.Skills.First(s => s.Id == m.Id);
                skill.Name = m.Name;
                skill.Profession = m.Profession;
                skill.MaxLevel = m.MaxLevel;
                skill.LaborReducePercent = m.LaborReducePercent;
                ApplyLocalizedName(skill.LocalizedName, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                ctx.Skills.Update(skill);
            }
        });
    }

    public async Task DeleteSkillAsync(Server server, Guid skillId)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var skill = sd.Skills.First(s => s.Id == skillId);
            DetachAndQueueDelete(ctx, skill, skill.Id);
        });
    }

    // ----- CraftingTable -----

    public async Task CreateOrUpdateCraftingTableAsync(Server server, CraftingTableEditModel m)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var pluginModules = sd.PluginModules
                .Where(pm => m.PluginModuleIds.Contains(pm.Id))
                .ToList();

            if (IsNew(m.Id))
            {
                var localizedName = NewLocalizedField(ctx, sd, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                var craftingTable = new CraftingTable
                {
                    Name = m.Name,
                    LocalizedName = localizedName,
                    PluginModules = pluginModules,
                    Server = sd,
                };
                sd.CraftingTables.Add(craftingTable);
                ctx.CraftingTables.Add(craftingTable);
            }
            else
            {
                var craftingTable = sd.CraftingTables.First(c => c.Id == m.Id);
                craftingTable.Name = m.Name;
                ApplyLocalizedName(craftingTable.LocalizedName, m.LocalizedNameEnUs, m.LocalizedNameCurrent);

                // Diff the M:M instead of reassigning the collection (see RefreshCraftingTable in
                // ImportDataService.Crud.cs): bulk replacement emits join-row DELETEs that can hit
                // already-removed rows and throw DbUpdateConcurrencyException.
                var newSet = new HashSet<PluginModule>(pluginModules);
                var oldSet = new HashSet<PluginModule>(craftingTable.PluginModules);

                foreach (var pm in craftingTable.PluginModules.ToList())
                {
                    if (!newSet.Contains(pm))
                    {
                        craftingTable.PluginModules.Remove(pm);
                    }
                }

                foreach (var pm in pluginModules)
                {
                    if (!oldSet.Contains(pm))
                    {
                        craftingTable.PluginModules.Add(pm);
                    }
                }

                ctx.CraftingTables.Update(craftingTable);
            }
        });
    }

    public async Task DeleteCraftingTableAsync(Server server, Guid id)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var craftingTable = sd.CraftingTables.First(c => c.Id == id);
            DetachAndQueueDelete(ctx, craftingTable, craftingTable.Id);
        });
    }

    // ----- PluginModule -----

    public async Task CreateOrUpdatePluginModuleAsync(Server server, PluginModuleEditModel m)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var skill = m.SkillId is not null ? sd.Skills.First(s => s.Id == m.SkillId) : null;

            if (IsNew(m.Id))
            {
                var localizedName = NewLocalizedField(ctx, sd, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                var pluginModule = new PluginModule
                {
                    Name = m.Name,
                    LocalizedName = localizedName,
                    PluginType = m.PluginType,
                    Percent = m.Percent,
                    Skill = skill,
                    SkillPercent = m.SkillPercent,
                    Server = sd,
                };
                sd.PluginModules.Add(pluginModule);
                ctx.PluginModules.Add(pluginModule);
            }
            else
            {
                var pluginModule = sd.PluginModules.First(p => p.Id == m.Id);
                pluginModule.Name = m.Name;
                pluginModule.PluginType = m.PluginType;
                pluginModule.Percent = m.Percent;
                pluginModule.Skill = skill;
                pluginModule.SkillPercent = m.SkillPercent;
                ApplyLocalizedName(pluginModule.LocalizedName, m.LocalizedNameEnUs, m.LocalizedNameCurrent);
                ctx.PluginModules.Update(pluginModule);
            }
        });
    }

    public async Task DeletePluginModuleAsync(Server server, Guid id)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async ctx =>
        {
            var sd = await serverDbService.GetServerWithData(server.Id, ctx);
            ctx.Attach(sd);

            var pluginModule = sd.PluginModules.First(p => p.Id == id);
            DetachAndQueueDelete(ctx, pluginModule, pluginModule.Id);
        });
    }

    // ----- Helpers -----

    private static bool IsNew(Guid? id) => id is null || id == Guid.Empty;

    // Detach from the tracker, then queue a SQL DELETE for after SaveChanges. Mirrors
    // ImportDataService.Crud.cs:DetachAndQueueDelete — prevents the tracker from racing the
    // DB-level cascade and throwing DbUpdateConcurrencyException on 0 rows affected.
    private static void DetachAndQueueDelete<T>(EcoCraftDbContext ctx, T entity, Guid id) where T : class
    {
        var entry = ctx.Entry(entity);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
        ctx.QueueDelete<T>(id);
    }

    // Track the new row as Added right away (Add it to the ctx immediately). Otherwise a later
    // ctx.X.Update(parent) would walk the graph and tag this untracked-but-keyed entity as
    // Modified -> UPDATE on a non-existent row -> concurrency error. Same rationale as
    // TranslationsToLocalizedField in ImportDataService.Helpers.cs.
    private LocalizedField NewLocalizedField(EcoCraftDbContext ctx, Server server, string enUs, string current)
    {
        var localizedField = new LocalizedField { Server = server };
        ctx.Add(localizedField);
        ApplyLocalizedName(localizedField, enUs, current);
        return localizedField;
    }

    private DynamicValue NewDynamicValue(EcoCraftDbContext ctx, Server server, decimal baseValue)
    {
        var dynamicValue = new DynamicValue { BaseValue = baseValue, Server = server };
        ctx.DynamicValues.Add(dynamicValue);
        return dynamicValue;
    }

    // Apply the two editable name columns coming from a data-editor dialog: en_US (the displayed
    // fallback) is always written; the current UI language column is written too when it differs from
    // en_US. Every OTHER language column is left untouched, so editing the French name never clobbers
    // the English (or any other) translation. The language->column switch mirrors
    // ImportDataService.Helpers.cs:TranslationsToLocalizedField.
    private void ApplyLocalizedName(LocalizedField localizedField, string enUs, string current)
    {
        SetColumn(localizedField, LanguageCode.en_US, enUs);
        if (localizationService.CurrentLanguageCode != LanguageCode.en_US)
        {
            SetColumn(localizedField, localizationService.CurrentLanguageCode, current);
        }
    }

    private static void SetColumn(LocalizedField lf, LanguageCode code, string value)
    {
        switch (code)
        {
            case LanguageCode.en_US: lf.en_US = value; break;
            case LanguageCode.fr: lf.fr = value; break;
            case LanguageCode.es: lf.es = value; break;
            case LanguageCode.de: lf.de = value; break;
            case LanguageCode.ko: lf.ko = value; break;
            case LanguageCode.pt_BR: lf.pt_BR = value; break;
            case LanguageCode.zh_Hans: lf.zh_Hans = value; break;
            case LanguageCode.ru: lf.ru = value; break;
            case LanguageCode.it: lf.it = value; break;
            case LanguageCode.pt_PT: lf.pt_PT = value; break;
            case LanguageCode.hu: lf.hu = value; break;
            case LanguageCode.ja: lf.ja = value; break;
            case LanguageCode.nn: lf.nn = value; break;
            case LanguageCode.pl: lf.pl = value; break;
            case LanguageCode.nl: lf.nl = value; break;
            case LanguageCode.ro: lf.ro = value; break;
            case LanguageCode.da: lf.da = value; break;
            case LanguageCode.cs: lf.cs = value; break;
            case LanguageCode.sv: lf.sv = value; break;
            case LanguageCode.uk: lf.uk = value; break;
            case LanguageCode.el: lf.el = value; break;
            case LanguageCode.ar_sa: lf.ar_sa = value; break;
            case LanguageCode.vi: lf.vi = value; break;
            case LanguageCode.tr: lf.tr = value; break;
            default: throw new ArgumentException($"Unsupported LanguageCode: {code}");
        }
    }
}
