namespace ecocraft.BuildingPlanner;

// Couleur d'affichage d'un matériau de construction dans le planificateur, extraite de l'icône du bloc
// (voir BlockColors.Generated.cs). Un bloc ajouté par un mod du serveur n'y figure pas : il prend la couleur
// de son tier, comme tous les matériaux avant l'extraction par icône.
public static partial class BlockColors
{
    static readonly string[] ByTier = ["#7d7d7d", "#c2a26a", "#8fa3b5", "#b0784a", "#6f8f9c", "#d4af37"];

    public static string For(string name, int tier) =>
        Generated.GetValueOrDefault(name) ?? ByTier[Math.Clamp(tier, 0, ByTier.Length - 1)];
}
