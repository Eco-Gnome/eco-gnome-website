using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Document → grille voxel : terrain à Y = 0, puis les ops du plan dans l'ordre (ArchLayer, une soustraction à z = 0
// creuse le terrain). Ni sol ni plafond implicites : comme en jeu, une pièce est une poche d'air fermée par des blocs.
// La grille couvre les ops, les niveaux et les objets (un objet plus haut que ce qui l'entoure dépasse, comme en jeu) ;
// les objets sont posés ensuite (ObjectPlacer).
public static class GridBuilder
{
    public static BuildContext Build(PlanDocument doc, Catalog catalog)
    {
        // Formes rognées à Architecture.Height (leur plafond, comme côté JS) ; la grille couvre le contenu réel, pas la hauteur réglée.
        var archTop = 0;
        foreach (var op in doc.Architecture.Ops) archTop = Math.Max(archTop, Math.Min(ArchShapes.MaxZ(op) + 1, doc.Architecture.Height));
        var sizeY = archTop;
        for (var k = 0; k < doc.Levels.Count; k++)
        {
            var baseY = doc.LevelBaseY(k);
            sizeY = Math.Max(sizeY, baseY + doc.LevelHeight(k) + 1);
            foreach (var o in doc.Levels[k].Objects)
            {
                var info = catalog.GetObject(o.Type);
                if (info is null || info.Cells.Count == 0) continue;
                sizeY = Math.Max(sizeY, baseY + (o.Z ?? 1) + info.Cells.Max(c => c.Offset.Y) - Math.Min(0, info.Cells.Min(c => c.Offset.Y)));
            }
        }
        sizeY += 2;

        var grid = new VoxelGrid(doc.Grid.Width, sizeY, doc.Grid.Depth);
        var ctx = new BuildContext { Document = doc, Catalog = catalog, Grid = grid };
        if (doc.Levels.All(l => l.Rooms.Count == 0) && doc.Levels.Any(l => l.Objects.Count > 0)) ctx.Issues.Add(PlanIssue.Warning("NoRoomDefined", []));

        for (var z = 0; z < doc.Grid.Depth; z++)
        for (var x = 0; x < doc.Grid.Width; x++)
            grid.Set(new Vec3i(x, 0, z), Voxel.Terrain);
        ArchLayer.Evaluate(doc, archTop).MergeInto(ctx);

        grid.RecomputeTopSolid();
        return ctx;
    }
}
