using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Pose les objets dans la grille : rotation, hauteur automatique dans leur niveau, empilement (CanBeOnSurface sur
// HasTableSurface, aucune cellule posée, comme le jeu), creusement des portes dans les murs, blocages et support.
public static class ObjectPlacer
{
    public const int MaxAttachDepth = 3;

    public static void PlaceAll(BuildContext ctx)
    {
        var doc = ctx.Document;
        var byId = new Dictionary<string, PlacedObject>(StringComparer.Ordinal);

        foreach (var (level, o) in doc.AllObjects())
        {
            var info = ctx.Catalog.GetObject(o.Type);
            var placed = new PlacedObject { Index = ctx.Objects.Count, Level = level, Doc = o, Info = info, Rotation = o.Rotation & 3 };
            ctx.Objects.Add(placed);
            if (!string.IsNullOrWhiteSpace(o.Id)) byId[o.Id] = placed;

            if (info is null) { ctx.Issues.Add(PlanIssue.Warning("UnknownObjectType", [o.Type], new GridPoint { X = o.X, Y = o.Y }, objectId: o.Id)); continue; }
            if (info.IsDefaultOccupancy) ctx.Issues.Add(PlanIssue.Info("DefaultOccupancy", [o.Type], objectId: o.Id));
        }

        // Objets au sol d'abord (ordre du document), puis les empilés dès que leur support est résolu.
        foreach (var p in ctx.Objects.Where(p => p.Info is not null && p.Doc.AttachedTo is null)) PlaceOnGround(ctx, p);

        var pending = ctx.Objects.Where(p => p.Info is not null && p.Doc.AttachedTo is not null).ToList();
        var progress = true;
        while (pending.Count > 0 && progress)
        {
            progress = false;
            foreach (var p in pending.ToList())
            {
                if (!byId.TryGetValue(p.Doc.AttachedTo!, out var parent)) { ctx.Issues.Add(PlanIssue.Warning("AttachParentMissing", [p.Doc.Id], objectId: p.Doc.Id)); pending.Remove(p); progress = true; continue; }
                if (parent.Info is null || (!parent.Placed && parent.Doc.AttachedTo is null)) { ctx.Issues.Add(PlanIssue.Warning("AttachParentMissing", [p.Doc.Id], objectId: p.Doc.Id)); pending.Remove(p); progress = true; continue; }
                if (parent.Doc.AttachedTo is not null && !parent.Placed) continue; // parent lui-même en attente

                Attach(ctx, p, parent);
                pending.Remove(p);
                progress = true;
            }
        }
        foreach (var p in pending) ctx.Issues.Add(PlanIssue.Warning("AttachParentMissing", [p.Doc.Id], objectId: p.Doc.Id));

        ctx.Grid.RecomputeTopSolid();
    }

    private static void Attach(BuildContext ctx, PlacedObject p, PlacedObject parent)
    {
        var info = p.Info!;
        if (!parent.Info!.HasTableSurface || !info.CanBeOnSurface) { ctx.Issues.Add(PlanIssue.Warning("AttachNotAllowed", [info.Name, parent.Info.Name], objectId: p.Doc.Id)); return; }
        if (parent.Depth + 1 > MaxAttachDepth) { ctx.Issues.Add(PlanIssue.Warning("AttachTooDeep", [info.Name, MaxAttachDepth.ToString()], objectId: p.Doc.Id)); return; }

        p.ParentIndex = parent.Index;
        p.Depth = parent.Depth + 1;
        p.Origin = new Vec3i(p.Doc.X, parent.Origin.Y + parent.MaxDy + 1, p.Doc.Y);
        p.MaxDy = 0;
        p.Placed = true;   // attaché : aucune cellule posée ni vérifiée (HierarchyComponent), hérite de la pièce du parent
    }

    private static void PlaceOnGround(BuildContext ctx, PlacedObject p)
    {
        var info = p.Info!;
        var grid = ctx.Grid;
        var levelBase = ctx.Document.LevelBaseY(p.Level);
        var levelTop = levelBase + ctx.Document.LevelHeight(p.Level);
        var rotated = info.Cells.Select(c => (Offset: Geometry.Rotate(c.Offset, p.Rotation), c.Kind)).ToList();
        var structure = rotated.Where(c => c.Kind is OccupancyKind.Occupied or OccupancyKind.Wall or OccupancyKind.Solid).ToList();
        if (structure.Count == 0) structure = [(Vec3i.Zero, OccupancyKind.Occupied)];

        var originOnWall = BuildContext.IsWallBlock(grid.Get(new Vec3i(p.Doc.X, levelBase + 1, p.Doc.Y)));
        p.IsDoorCarving = originOnWall && info.HasWallCells;

        var minDy = structure.Min(c => c.Offset.Y);
        p.MaxDy = structure.Max(c => c.Offset.Y);

        int originY;
        if (p.Doc.Z is { } z) originY = levelBase + z;
        else
        {
            // Posé sur le sol du niveau : première couche, en partant du bas, où toute l'empreinte est de l'air posé sur
            // du solide, sans sortir du niveau (une dalle absente ne doit pas faire grimper l'objet sur le toit).
            // Une porte s'ancre au sol.
            var footprintCells = p.IsDoorCarving ? structure.Where(c => c.Kind != OccupancyKind.Wall).ToList() : structure;
            var columns = footprintCells.Select(c => (X: p.Doc.X + c.Offset.X, Z: p.Doc.Y + c.Offset.Z)).Distinct().ToList();
            // Niveau du sol = plus basse couche d'air-sur-solide parmi les colonnes ; une colonne plus haute (mur,
            // autre objet) sera signalée comme blocage au lieu de faire grimper l'objet.
            var baseY = levelBase + 1;
            if (!p.IsDoorCarving)
            {
                var lowest = int.MaxValue;
                foreach (var c in columns)
                {
                    for (var y = levelBase + 1; y <= levelTop && y < grid.SizeY; y++)
                    {
                        if (grid.Get(new Vec3i(c.X, y, c.Z)).Kind == VoxelKind.Air && grid.Get(new Vec3i(c.X, y - 1, c.Z)).IsSolid) { lowest = Math.Min(lowest, y); break; }
                    }
                }
                if (lowest != int.MaxValue) baseY = lowest;
            }
            originY = baseY - minDy;
        }
        p.Origin = new Vec3i(p.Doc.X, originY, p.Doc.Y);

        // Vérification de toutes les cellules avant d'en poser une seule.
        var targets = new List<(Vec3i Pos, OccupancyKind Kind)>();
        foreach (var (offset, kind) in structure)
        {
            var pos = p.Origin + offset;
            if (!grid.InBounds(pos)) { ctx.Issues.Add(PlanIssue.Error("OutOfGrid", [info.Name], new GridPoint { X = p.Doc.X, Y = p.Doc.Y }, objectId: p.Doc.Id)); return; }

            var existing = grid.Get(pos);
            if (existing.Kind != VoxelKind.Air)
            {
                var carvable = p.IsDoorCarving && kind == OccupancyKind.Wall && BuildContext.IsWallBlock(existing);
                if (!carvable)
                {
                    ReportBlocked(ctx, p, existing, pos, minDy, levelBase);
                    return;
                }
            }
            targets.Add((pos, kind));
        }

        if (info.RequiresSupportBelow)
        {
            var unsupported = targets.Where(t => t.Pos.Y - p.Origin.Y == minDy && !grid.Get(t.Pos - new Vec3i(0, 1, 0)).IsSolid).ToList();
            if (unsupported.Count > 0) ctx.Issues.Add(PlanIssue.Warning("NoSupportBelow", [info.Name], new GridPoint { X = p.Doc.X, Y = p.Doc.Y }, objectId: p.Doc.Id));
        }

        foreach (var (pos, kind) in targets)
        {
            var voxelKind = kind switch
            {
                OccupancyKind.Wall => VoxelKind.ObjectWall,
                OccupancyKind.Solid => VoxelKind.ObjectSolid,
                _ => VoxelKind.ObjectOccupied,
            };
            grid.Set(pos, new Voxel { Kind = voxelKind, MaterialIndex = -1, ObjectIndex = p.Index });
            p.Cells.Add((pos, kind));
        }
        p.Placed = true;
    }

    private static void ReportBlocked(BuildContext ctx, PlacedObject p, Voxel blocker, Vec3i pos, int minDy, int levelBase)
    {
        var info = p.Info!;
        var cell = new GridPoint { X = pos.X, Y = pos.Z };
        if (blocker.Kind == VoxelKind.Block && (blocker.IsCeiling || (blocker.IsFloor && pos.Y > levelBase)))
        {
            // Un plafond (ou la dalle de l'étage) bloque : la pièce doit avoir au moins autant de couches d'air que
            // le sommet de l'objet, compté depuis la dalle du niveau.
            var objectHeight = p.MaxDy - minDy + 1;
            var requiredHeight = p.Origin.Y + p.MaxDy - levelBase;
            ctx.Issues.Add(PlanIssue.Error("BlockedByCeiling", [info.Name, objectHeight.ToString(), requiredHeight.ToString()], cell, objectId: p.Doc.Id));
            return;
        }

        var blockerName = blocker.Kind switch
        {
            VoxelKind.Terrain => "terrain",
            VoxelKind.Block => ctx.Materials[blocker.MaterialIndex].Name,
            _ when blocker.ObjectIndex >= 0 => ctx.Objects[blocker.ObjectIndex].Doc.Id,
            _ => "?",
        };
        ctx.Issues.Add(PlanIssue.Error("Blocked", [info.Name, blockerName], cell, objectId: p.Doc.Id));
    }
}
