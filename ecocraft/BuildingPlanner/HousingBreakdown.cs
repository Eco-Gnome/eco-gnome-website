using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

/// <summary>Une réduction appliquée par le jeu à la valeur housing d'une pièce, dans l'ordre du calcul :
/// « support » (catégorie de soutien ramenée à x % de la principale), « tier » (soft-cap des matériaux),
/// « rank » (n-ième pièce d'une catégorie sur la propriété), « category » (catégorie limitée à x % du reste de la propriété).</summary>
public sealed record HousingStep(string Kind, string Category, string Primary, float Before, float After, float Percent, float Reference, int Rank = 0, int Residents = 0)
{
    public float Factor => Before > 0 ? After / Before : 1f;
}

public static class HousingBreakdown
{
    public static List<HousingStep> RoomSteps(RoomHousingResult room, PropertyHousingResult? property, string roomId, Func<string, float> capPercent)
    {
        var steps = new List<HousingStep>();
        foreach (var kv in room.ValueByCategory)
        {
            if (kv.Key == room.PrimaryCategory || !room.RawValueByCategory.TryGetValue(kv.Key, out var raw) || raw <= kv.Value + 0.005f) continue;
            steps.Add(new HousingStep("support", kv.Key, room.PrimaryCategory, raw, kv.Value, room.PrimaryValue > 0 ? kv.Value / room.PrimaryValue : 0f, room.PrimaryValue));
        }
        if (room.CappedByTier) steps.Add(new HousingStep("tier", room.PrimaryCategory, room.PrimaryCategory, room.TotalBeforeCap, room.Value, room.TierVal, room.TierSoftCap));
        if (property is null) return steps;
        var contribution = property.Rooms.FirstOrDefault(r => r.RoomId == roomId);
        if (contribution is not null && contribution.Multiplier < 0.999f)
            steps.Add(new HousingStep("rank", room.PrimaryCategory, room.PrimaryCategory, contribution.RoomValue, contribution.Contribution, contribution.Multiplier, 0f, contribution.Rank + 1, property.Residents));
        if (property.CapAppliedByCategory.TryGetValue(room.PrimaryCategory, out var before))
            steps.Add(new HousingStep("category", room.PrimaryCategory, room.PrimaryCategory, before, property.ByCategory.GetValueOrDefault(room.PrimaryCategory), capPercent(room.PrimaryCategory), property.UncappedTotal));
        return steps;
    }

    /// <summary>Part d'un objet de la catégorie donnée qui compte au final (prorata : le jeu applique chaque limite au total, pas objet par objet).</summary>
    public static float ObjectFactor(IEnumerable<HousingStep> steps, string objectCategory)
        => steps.Where(s => s.Kind != "support" || s.Category == objectCategory).Aggregate(1f, (f, s) => f * s.Factor);

    /// <summary>Ce que la pièce apporte réellement à la propriété (après rang et limite de catégorie ; sa valeur inclut déjà soutiens et soft-cap).</summary>
    public static float RoomFactor(IEnumerable<HousingStep> steps)
        => steps.Where(s => s.Kind is "rank" or "category").Aggregate(1f, (f, s) => f * s.Factor);
}
