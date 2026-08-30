using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Réplique de StandardPropertyValue + ResidencyPropertyValue : par catégorie, pièces triées par valeur
// décroissante avec × rate^(i / résidents) (division entière), catégories plafonnées à x % du reste,
// somme, × multiplicateur d'occupation. Culture ×1 et boost admin 0 en v1.
public static class PropertyScorer
{
    public static PropertyHousingResult Score(IReadOnlyList<RoomHousingResult> rooms, int residents, float? target, Catalog catalog)
    {
        var rules = catalog.Housing;
        var build = catalog.Build;
        var residentsNumber = residents <= 0 ? 1 : residents;
        var result = new PropertyHousingResult { Residents = residentsNumber, Target = target };

        var groups = rooms
            .Where(r => !r.Negated || true)  // une pièce industrielle vaut 0 mais reste listée
            .GroupBy(r => r.PrimaryCategory)
            .OrderBy(g => (rules.GetCategory(g.Key)?.CapToPercentOfRestOfProperty ?? 0f) > 0f)
            .ToList();

        var roomSums = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var sum = 0f;
            var i = 0;
            foreach (var room in group.OrderByDescending(r => r.Value))
            {
                var mult = EcoMath.DiminishingReturn(build.RoomCategoryDiminishingReturnRate, i / residentsNumber);
                var contribution = room.Value * mult;
                result.Rooms.Add(new RoomContribution { RoomId = room.RoomId, Category = group.Key, RoomValue = room.Value, Multiplier = mult, Contribution = contribution, Rank = i });
                sum += contribution;
                i++;
            }
            roomSums[group.Key] = sum;
        }

        var restOfHouse = roomSums.Where(x => (rules.GetCategory(x.Key)?.CapToPercentOfRestOfProperty ?? 0f) == 0f).Sum(x => x.Value);
        result.UncappedTotal = restOfHouse;
        foreach (var (name, sum) in roomSums.ToList())
        {
            var cat = rules.GetCategory(name);
            if (cat is null || cat.CapToPercentOfRestOfProperty <= 0f) continue;
            var maxVal = EcoMath.Round2(cat.CapToPercentOfRestOfProperty * restOfHouse);
            if (sum > maxVal) { result.CapAppliedByCategory[name] = sum; roomSums[name] = maxVal; }
        }

        result.ByCategory = roomSums;
        var total = roomSums.Sum(x => x.Value);
        result.TotalBeforeOccupancy = total;
        result.OccupancyMultiplier = rules.GetOccupancyMultiplier(residentsNumber, build);
        result.Total = total * result.OccupancyMultiplier;
        return result;
    }
}
