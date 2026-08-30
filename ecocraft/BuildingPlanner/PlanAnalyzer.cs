using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Façade : Valider → Construire la grille → Poser → Pièces → Requirements → Housing → Coût. Fonction pure.
public static class PlanAnalyzer
{
    public static AnalysisResult Analyze(PlanDocument doc, Catalog catalog)
    {
        var validation = PlanValidator.Validate(doc);
        if (validation.Any(i => i.Severity == IssueSeverity.Error))
            return new AnalysisResult { Issues = validation, Blocked = true, HousingRulesAreDefaults = catalog.Housing.IsDefault };

        var ctx = GridBuilder.Build(doc, catalog);
        ObjectPlacer.PlaceAll(ctx);

        // Détection des pièces et appartenance des objets (≥ 51 % des cellules posées ; empilés → pièce du parent).
        var roomStats = new Dictionary<string, RoomStats>(StringComparer.Ordinal);
        var roomSeeds = new Dictionary<string, Vec3i>(StringComparer.Ordinal);
        var roomIdByObjectId = new Dictionary<string, string>(StringComparer.Ordinal);
        var rooms = new List<RoomAnalysis>();
        foreach (var (level, room) in doc.AllRooms())
        {
            var baseY = doc.LevelBaseY(level);
            var seed = Geometry.PlanToEco(room.Seed.X, room.Seed.Y, baseY + 1);
            var stats = RoomChecker.GetRoomStats(ctx, seed);
            roomStats[room.Id] = stats;
            roomSeeds[room.Id] = seed;

            var failLevel = stats.FailCell is { } fc ? doc.LevelIndexAtY(fc.Y) : (int?)null;
            var analysis = new RoomAnalysis
            {
                RoomId = room.Id,
                Name = room.Name,
                Contained = stats.Contained,
                FailCode = stats.FailCode,
                FailCell = stats.FailCell is { } c ? new GridPoint { X = c.X, Y = c.Z } : null,
                FailHeight = stats.FailCell is { } f ? f.Y - doc.LevelBaseY(failLevel!.Value) : null,
                FailLevel = failLevel,
                Volume = stats.Volume,
                WallCount = stats.WallCount,
                WallTierComposition = new Dictionary<float, int>(stats.WallTierComposition),
                AverageTier = stats.AverageTier,
                EmptyEdgeCount = stats.EmptyEdgeCount,
                AverageTierWithoutEmptyEdges = stats.AverageTierExcludingEmptyEdges(),
                FootprintCellCount = ctx.RoomFootprints.GetValueOrDefault(room.Id)?.Count ?? 0,
                Height = doc.RoomHeight(level, room),
            };
            rooms.Add(analysis);

            if (!stats.Contained)
            {
                var args = stats.FailCode == "NoCeiling" && analysis.FailHeight is { } fh ? new[] { room.Name, fh.ToString() } : new[] { room.Name };
                ctx.Issues.Add(PlanIssue.Error(stats.FailCode ?? "RoomInvalid", args, analysis.FailCell, roomId: room.Id, level: failLevel));
                continue;
            }

            if (stats.EmptyEdgeCount > 0)
                ctx.Issues.Add(PlanIssue.Info("EmptyEdges", [room.Name, stats.EmptyEdgeCount.ToString(), analysis.AverageTierWithoutEmptyEdges.ToString("0.##")], roomId: room.Id));

            foreach (var obj in ctx.Objects.Where(o => o.Placed && o.Doc.AttachedTo is null && RoomChecker.IsContained(o, stats)))
            {
                // Une porte entre deux pièces appartient aux deux ; pour les requirements le jeu évalue la première.
                roomIdByObjectId.TryAdd(obj.Doc.Id, room.Id);
                analysis.ObjectIds.Add(obj.Doc.Id);
            }
        }

        // Pièces de niveaux différents reliées (ouverture dans la dalle, dalle absente) : un seul espace dans le jeu,
        // qui serait compté deux fois ici.
        var allRooms = doc.AllRooms().ToList();
        for (var i = 0; i < allRooms.Count; i++)
        for (var j = i + 1; j < allRooms.Count; j++)
        {
            var (levelA, a) = allRooms[i];
            var (levelB, b) = allRooms[j];
            if (levelA == levelB || !roomStats[a.Id].Contained || !roomStats[a.Id].EmptySpace.Contains(roomSeeds[b.Id])) continue;
            ctx.Issues.Add(PlanIssue.Warning("RoomsShareSpace", [b.Name, a.Name], roomId: b.Id));
        }

        // Empilés : pièce du parent (récursif, la profondeur est bornée à 3).
        var objectsById = ctx.Objects.ToDictionary(o => o.Doc.Id, o => o, StringComparer.Ordinal);
        foreach (var obj in ctx.Objects.Where(o => o.Placed && o.Doc.AttachedTo is not null))
        {
            var current = obj;
            string? roomId = null;
            for (var depth = 0; depth <= ObjectPlacer.MaxAttachDepth && current.Doc.AttachedTo is not null; depth++)
            {
                if (!objectsById.TryGetValue(current.Doc.AttachedTo, out var parent)) break;
                if (roomIdByObjectId.TryGetValue(parent.Doc.Id, out roomId)) break;
                current = parent;
            }
            if (roomId is null) continue;
            roomIdByObjectId[obj.Doc.Id] = roomId;
            rooms.First(r => r.RoomId == roomId).ObjectIds.Add(obj.Doc.Id);
        }
        foreach (var obj in ctx.Objects) obj.RoomId = roomIdByObjectId.GetValueOrDefault(obj.Doc.Id);

        var tables = RoomRequirementChecker.Check(ctx, roomStats, roomIdByObjectId);
        foreach (var table in tables)
        {
            if (table.RoomId is not null) rooms.First(r => r.RoomId == table.RoomId).Tables.Add(table);
            if (!table.InRoom) { ctx.Issues.Add(PlanIssue.Error("TableNotInRoom", [table.Type], objectId: table.ObjectId)); continue; }
            if (!table.ContainmentOk) ctx.Issues.Add(PlanIssue.Error("TableNotContained", [table.Type], objectId: table.ObjectId, roomId: table.RoomId));
            if (!table.TierOk) ctx.Issues.Add(PlanIssue.Error("TableTierTooLow", [table.Type, table.EffectiveTier?.ToString("0.##") ?? "", table.RoomTier.ToString("0.##"), table.TierGap.ToString("0.##")], objectId: table.ObjectId, roomId: table.RoomId));
            else if (!table.ModulesOk) ctx.Issues.Add(PlanIssue.Warning("TableModulesInactive", [table.Type, table.EffectiveTier?.ToString("0.##") ?? "", table.RoomTier.ToString("0.##")], objectId: table.ObjectId, roomId: table.RoomId));
            if (!table.VolumeOk) ctx.Issues.Add(PlanIssue.Error("TableVolumeExceeded", [table.Type, table.RoomVolumeUsed.ToString(), table.RoomVolume.ToString(), table.VolumeGap.ToString()], objectId: table.ObjectId, roomId: table.RoomId));
        }
        foreach (var room in rooms) room.RequiredVolumeTotal = room.Tables.FirstOrDefault()?.RoomVolumeUsed ?? 0;

        // Housing : valeur de meuble = base × D_propriété^(n−1), n = objets du même type sur toute la propriété.
        var typeCounts = ctx.Objects.Where(o => o.Placed && o.Info?.Housing is not null).GroupBy(o => o.Info!.Name).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var roomHousing = new List<RoomHousingResult>();
        foreach (var (_, room) in allRooms)
        {
            var stats = roomStats[room.Id];
            var analysis = rooms.First(r => r.RoomId == room.Id);
            if (!stats.Contained) continue;

            var furnishings = analysis.ObjectIds
                .Select(id => objectsById[id])
                .Where(o => o.Info?.Housing is not null)
                .Select(o =>
                {
                    var h = o.Info!.Housing!;
                    var n = typeCounts.GetValueOrDefault(o.Info.Name, 1);
                    var propertyMult = h.DiminishingMultiplierAcrossFullProperty == 1f ? 1f : MathF.Pow(h.DiminishingMultiplierAcrossFullProperty, n - 1);
                    return new HousingScorer.FurnishingInput(o, h, h.BaseValue * propertyMult);
                })
                .ToList();

            var housing = HousingScorer.Score(room.Id, room.Name, furnishings, stats, room.LockCategory, doc.Analysis.PropertyType, catalog, out var housingIssues);
            analysis.Housing = housing;
            roomHousing.Add(housing);
            ctx.Issues.AddRange(housingIssues);
        }

        var property = PropertyScorer.Score(roomHousing, doc.Analysis.Residents, doc.Analysis.TargetHousing, catalog);
        if (catalog.Housing.IsDefault) ctx.Issues.Add(PlanIssue.Info("HousingConfigMissing", []));

        // Références absentes du catalogue de ce serveur (plan partagé depuis un serveur aux mods différents) : un bilan en tête.
        var unknown = ctx.Materials.Where(m => !m.Known).Select(m => m.Name)
            .Concat(ctx.Objects.Where(o => o.Info is null).Select(o => o.Doc.Type))
            .Concat(ctx.Issues.Where(i => i.Code is "UnknownHousingCategory" or "UnknownLockCategory").Select(i => i.Args[1]))
            .Distinct(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0) ctx.Issues.Insert(0, PlanIssue.Warning("IncompatibleReferences", [unknown.Count.ToString(), string.Join(", ", unknown)]));

        var (materials, objectCounts) = MaterialCostCalculator.Compute(ctx);

        var placed = ctx.Objects.Select(o => new PlacedObjectResult
        {
            Id = o.Doc.Id,
            Type = o.Doc.Type,
            Known = o.Info is not null,
            Placed = o.Placed,
            Origin = o.Origin,
            Rotation = o.Rotation,
            Cells = o.Cells.Select(c => c.Pos).ToList(),
            AttachedTo = o.Doc.AttachedTo,
            RoomId = o.RoomId,
            IsDoor = o.IsDoorCarving,
        }).ToList();

        // Niveau des problèmes non localisés explicitement : celui de leur pièce ou de leur objet.
        var issues = validation.Concat(ctx.Issues)
            .Select(i => i.Level is not null ? i : i with { Level = doc.FindRoom(i.RoomId)?.Level ?? doc.FindObject(i.ObjectId)?.Level })
            .OrderBy(i => i.Severity == IssueSeverity.Error ? 0 : i.Severity == IssueSeverity.Warning ? 1 : 2)
            .ToList();

        return new AnalysisResult
        {
            Issues = issues,
            Rooms = rooms,
            Tables = tables,
            Housing = property,
            Materials = materials,
            ObjectCounts = objectCounts,
            Objects = placed,
            GridSizeY = ctx.Grid.SizeY,
            HousingRulesAreDefaults = catalog.Housing.IsDefault,
        };
    }
}
