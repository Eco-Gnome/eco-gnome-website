using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Réplique de StandardFurnishedRoomValue (valeur d'une pièce) : catégorie primaire estimée, exclusions,
// rendements décroissants par type dans la pièce, plafond des catégories de support, arrondi à 2 décimales
// par catégorie, puis cap composite par tier de matériaux. Peinture, pollution, efficacité = neutres en v1.
public static class HousingScorer
{
    public sealed record FurnishingInput(PlacedObject Object, HousingValueInfo Housing, float FurnishingValue);

    public static RoomHousingResult Score(string roomId, string roomName, IReadOnlyList<FurnishingInput> furnishings, RoomStats stats, string? lockCategory, string propertyType, Catalog catalog, out List<PlanIssue> issues)
    {
        issues = [];
        var rules = catalog.Housing;
        var result = new RoomHousingResult { RoomId = roomId };
        var all = furnishings.Where(f => rules.GetCategory(f.Housing.Category) is not null).ToList();
        foreach (var f in furnishings.Except(all)) issues.Add(PlanIssue.Warning("UnknownHousingCategory", [f.Object.Info!.Name, f.Housing.Category], roomId: roomId, objectId: f.Object.Doc.Id));
        if (!string.IsNullOrEmpty(lockCategory) && rules.GetCategory(lockCategory) is null) issues.Add(PlanIssue.Warning("UnknownLockCategory", [roomName, lockCategory], roomId: roomId));

        var tier = ComputeRoomTier(stats, rules);
        result.TierVal = tier.TierVal;
        result.TierSoftCap = tier.SoftCap;
        result.TierHardCap = tier.HardCap;

        if (all.Count == 0) { result.PrimaryCategory = HousingRules.UncategorizedName; return result; }

        // Un seul objet d'une catégorie qui annule (Industrial) → pièce industrielle, valeur 0.
        var negating = all.FirstOrDefault(f => rules.GetCategory(f.Housing.Category)!.NegatesValue);
        if (negating is not null)
        {
            result.Negated = true;
            result.PrimaryCategory = negating.Housing.Category;
            result.Objects = all.Select(f => Line(f, 0f, 0f, excluded: true)).ToList();
            issues.Add(PlanIssue.Warning("HousingNegated", [negating.Object.Info!.Name, negating.Housing.Category, roomName], roomId: roomId, objectId: negating.Object.Doc.Id));
            return result;
        }

        RoomCategoryInfo primary;
        var locked = rules.GetCategory(lockCategory);
        if (locked is not null) { primary = locked; result.CategoryLocked = true; }
        else
        {
            var validCats = rules.Categories.Where(c => c.AffectsPropertyTypes.Contains(propertyType) && c.CanAutoChooseCategory).ToHashSet();
            primary = EstimateHighestCategory(all, validCats, rules, out var tie) ?? rules.Uncategorized;
            result.PrimaryTie = tie;
            if (tie) issues.Add(PlanIssue.Info("PrimaryCategoryTie", [primary.Name], roomId: roomId));
        }
        result.PrimaryCategory = primary.Name;

        var validForRoom = rules.Categories.Where(c => c.Name == primary.Name || c.SupportForAnyRoomType || primary.SupportingRoomCategoryNames.Contains(c.Name)).Select(c => c.Name).ToHashSet();
        var included = all.Where(f => validForRoom.Contains(f.Housing.Category)).ToList();
        var excluded = all.Where(f => !validForRoom.Contains(f.Housing.Category)).ToList();
        foreach (var f in excluded) issues.Add(PlanIssue.Info("HousingObjectExcluded", [f.Object.Info!.Name, f.Housing.Category, roomName, primary.Name], roomId: roomId, objectId: f.Object.Doc.Id));

        // Valeur par catégorie : objets triés par valeur décroissante, groupés par type, rang i → × D^i.
        var lines = new List<ObjectHousingLine>();
        var valuePerCategory = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var catGroup in included.GroupBy(f => f.Housing.Category))
        {
            var catValue = 0f;
            foreach (var typeGroup in catGroup.OrderByDescending(f => f.FurnishingValue).GroupBy(f => f.Housing.TypeForRoomLimit))
            {
                var i = 0;
                foreach (var f in typeGroup)
                {
                    var mult = EcoMath.DiminishingReturn(f.Housing.DiminishingReturnMultiplier, i);
                    var value = f.FurnishingValue * mult;
                    catValue += value;
                    lines.Add(Line(f, mult, value, excluded: false));
                    i++;
                }
            }
            valuePerCategory[catGroup.Key] = catValue;
        }
        lines.AddRange(excluded.Select(f => Line(f, 0f, 0f, excluded: true)));
        result.Objects = lines;
        result.RawValueByCategory = new Dictionary<string, float>(valuePerCategory);

        var primaryVal = valuePerCategory.GetValueOrDefault(primary.Name);
        result.PrimaryValue = primaryVal;
        var total = 0f;
        foreach (var entry in valuePerCategory.OrderByDescending(x => x.Key == primary.Name))
        {
            var valToUse = entry.Value;
            if (entry.Key != primary.Name)
            {
                var cat = rules.GetCategory(entry.Key)!;
                var maxAllowed = primaryVal * cat.GetMaxSupportPercentOfPrimary(primary);
                if (valToUse > maxAllowed) valToUse = maxAllowed;
            }
            var rounded = EcoMath.Round2(valToUse);
            result.ValueByCategory[entry.Key] = rounded;
            total += rounded;
        }
        result.TotalBeforeCap = total;

        // Peinture (PaintedBlockHousingBonus) et pollution : facteurs 1 en v1, affichés comme hypothèses.
        var capped = primary.ShouldCapFromRoomMaterials ? EcoMath.ApplyRoomTier(tier.SoftCap, tier.HardCap, tier.DiminishingReturnPercent, total) : total;
        result.Value = capped;
        result.CappedByTier = capped < total - 0.0001f;
        if (result.CappedByTier) issues.Add(PlanIssue.Info("HousingCapped", [total.ToString("0.##"), capped.ToString("0.##"), tier.SoftCap.ToString("0.##")], roomId: roomId));

        return result;
    }

    // EstimateHighestCategory : Σ valeurs de la catégorie + Σ supports min(Σ support, Σ cat × pct), sans rendements
    // décroissants (approximation du jeu, reproduite telle quelle). Égalité → premier dans l'ordre des catégories.
    private static RoomCategoryInfo? EstimateHighestCategory(List<FurnishingInput> all, HashSet<RoomCategoryInfo> validCats, HousingRules rules, out bool tie)
    {
        tie = false;
        var catSum = all.GroupBy(f => f.Housing.Category).ToDictionary(g => g.Key, g => g.Sum(f => f.FurnishingValue));
        var scored = new List<(RoomCategoryInfo Cat, float Score)>();
        foreach (var (name, sum) in catSum)
        {
            var cat = rules.GetCategory(name)!;
            if (!cat.CanBeRoomCategory || !validCats.Contains(cat)) continue;
            var support = catSum.Where(x => x.Key != name && cat.IsSupportedBy(rules.GetCategory(x.Key)!))
                .Sum(x => MathF.Min(x.Value, sum * rules.GetCategory(x.Key)!.GetMaxSupportPercentOfPrimary(cat)));
            scored.Add((cat, sum + support));
        }
        if (scored.Count == 0) return null;

        var best = scored[0];
        foreach (var s in scored.Skip(1)) if (s.Score > best.Score) best = s;
        tie = scored.Count(s => Math.Abs(s.Score - best.Score) < 0.0001f) > 1;
        return best.Cat;
    }

    // RoomTierUtils.CalcRoomTier : moyenne pondérée (nb de blocs) des RoomTier par tier entier de la composition.
    public static RoomTierInfo ComputeRoomTier(RoomStats stats, HousingRules rules)
    {
        var total = (float)stats.WallTierComposition.Values.Sum();
        if (total <= 0) return rules.GetRoomTier(0);

        float soft = 0, hard = 0, dim = 0, tierVal = 0;
        foreach (var entry in stats.WallTierComposition.OrderBy(x => x.Key))
        {
            var tier = rules.GetRoomTier((int)entry.Key);
            var percent = entry.Value / total;
            soft += percent * tier.SoftCap;
            hard += percent * tier.HardCap;
            dim += percent * tier.DiminishingReturnPercent;
            tierVal += percent * entry.Key;
        }
        return new RoomTierInfo { TierVal = tierVal, SoftCap = soft, HardCap = hard, DiminishingReturnPercent = dim };
    }

    private static ObjectHousingLine Line(FurnishingInput f, float mult, float value, bool excluded) => new()
    {
        ObjectId = f.Object.Doc.Id,
        Type = f.Object.Info!.Name,
        Category = f.Housing.Category,
        TypeForRoomLimit = f.Housing.TypeForRoomLimit,
        FurnishingValue = f.FurnishingValue,
        Multiplier = mult,
        Value = value,
        Excluded = excluded,
    };
}
