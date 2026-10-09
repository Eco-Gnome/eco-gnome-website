using ecocraft.Models;

namespace ecocraft.Services.EcoLink;

public static class EcoContextResolver
{
    /// <summary>Picks the context named by the mod: exact name first, then a unique prefix, the default one when empty.</summary>
    public static (DataContext? Context, string? Error) Resolve(IEnumerable<DataContext> dataContexts, string? name)
    {
        var contexts = dataContexts.Where(d => !d.IsShoppingList).ToList();

        if (string.IsNullOrWhiteSpace(name))
        {
            var defaultContext = contexts.FirstOrDefault(d => d.IsDefault) ?? contexts.FirstOrDefault();
            return defaultContext is null ? (null, "You have no context on Eco Gnome yet. Open Eco Gnome once to create it.") : (defaultContext, null);
        }

        // An exact name wins, so "Forge" can still be picked next to "Forge 2"
        var matches = contexts.Where(d => d.Name == name).ToList();
        if (matches.Count == 0) matches = contexts.Where(d => d.Name.StartsWith(name)).ToList();

        return matches.Count switch
        {
            0 => (null, "No context starts with the name you provided. Leave it empty to use the default context."),
            > 1 => (null, "Several contexts start with the name you provided. Please be more specific to select only one."),
            _ => (matches[0], null),
        };
    }
}
