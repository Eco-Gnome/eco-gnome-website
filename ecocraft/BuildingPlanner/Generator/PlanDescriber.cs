using System.Text;
using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Plan existant en texte compact pour le LLM (modification par l'IA) : niveaux, une ligne par op dans l'ordre d'évaluation,
// objets, pièces et problèmes de l'analyse, puis une vue de dessus par niveau (couche base + 1, une lettre par matériau).
// Une op cells n'est résumée que par sa boîte englobante : elle se supprime, se repeint ou se creuse, pas case par case.
public static class PlanDescriber
{
    public const int MaxMapCells = 40_000;   // au-delà (grands plans, nombreux niveaux), pas de vues de dessus
    private const int MaxIssues = 30;
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string Describe(PlanDocument doc, AnalysisResult? analysis)
    {
        var sb = new StringBuilder();
        var (w, d, h) = (doc.Grid.Width, doc.Grid.Depth, doc.Architecture.Height);
        sb.AppendLine($"Grid {w}x{d}, height {h} (x east, y south, z up; z 0 = terrain layer).");
        sb.AppendLine("Levels (base = z of the level's floor layer):");
        for (var k = 0; k < doc.Levels.Count; k++)
            sb.AppendLine($"- level {k}{(doc.Levels[k].Name.Length > 0 ? $" \"{doc.Levels[k].Name}\"" : "")}: base {doc.LevelBaseY(k)}, height {doc.LevelHeight(k)}");

        sb.AppendLine($"Ops, in painting order (a later op overwrites an earlier one; cut = carves air), {doc.Architecture.Ops.Count}:");
        foreach (var op in doc.Architecture.Ops) sb.AppendLine(Op(op));

        sb.AppendLine("Objects (x,y = anchor cell; cells = footprint):");
        var placed = (analysis?.Objects ?? []).DistinctBy(o => o.Id).ToDictionary(o => o.Id, StringComparer.Ordinal);
        for (var k = 0; k < doc.Levels.Count; k++)
        foreach (var o in doc.Levels[k].Objects)
        {
            var cells = placed.TryGetValue(o.Id, out var p) && p.Cells.Count > 0
                ? $" cells x {p.Cells.Min(c => c.X)}..{p.Cells.Max(c => c.X)} y {p.Cells.Min(c => c.Z)}..{p.Cells.Max(c => c.Z)} z {p.Cells.Min(c => c.Y)}..{p.Cells.Max(c => c.Y)}{(p.Placed ? "" : " (not placed)")}"
                : "";
            sb.AppendLine($"- {o.Id} {o.Type} level {k} at {o.X},{o.Y}{(o.Z is { } z ? $" z {z}" : "")} rot {o.Rotation}{cells}");
        }

        if (analysis is not null)
        {
            sb.AppendLine("Rooms (closed air pockets, as in the game):");
            var rooms = analysis.Rooms.DistinctBy(r => r.RoomId).ToDictionary(r => r.RoomId, StringComparer.Ordinal);
            foreach (var (k, room) in doc.AllRooms())
            {
                var line = $"- {room.Id} \"{room.Name}\" level {k} seed {room.Seed.X},{room.Seed.Y},{doc.LevelBaseY(k) + room.Seed.Z}";
                if (rooms.TryGetValue(room.Id, out var r))
                    line += r.Contained
                        ? $" closed, volume {r.Volume}, average tier {r.AverageTier:0.##}{(r.EmptyEdgeCount > 0 ? $", {r.EmptyEdgeCount} empty edges" : "")}{(r.Housing is { } hs ? $", {hs.PrimaryCategory} {hs.Value:0.#}" : "")}"
                        : $" not closed ({r.FailCode})";
                sb.AppendLine(line);
            }
            var issues = analysis.Issues.Where(i => i.Severity != IssueSeverity.Info).ToList();
            if (issues.Count > 0)
            {
                sb.AppendLine("Problems found by the planner:");
                foreach (var i in issues.Take(MaxIssues))
                    sb.AppendLine($"- {i.Severity} {i.Code}{(i.Args.Length > 0 ? " " + string.Join(", ", i.Args) : "")}{(i.RoomId is not null ? $" room {i.RoomId}" : "")}{(i.ObjectId is not null ? $" object {i.ObjectId}" : "")}{(i.Cell is not null ? $" at {i.Cell.X},{i.Cell.Y}" : "")}");
                if (issues.Count > MaxIssues) sb.AppendLine($"- … {issues.Count - MaxIssues} more");
            }
        }

        AppendMaps(sb, doc);
        return sb.ToString();
    }

    private static string Op(ArchOp op)
    {
        var sb = new StringBuilder($"{op.Id} {op.Kind}");
        if (op.Subtract) sb.Append(" cut");
        else sb.Append($" {op.Material}{(op.Form is { Length: > 0 } f ? " " + f : "")}{(op.Rot is > 0 and var r ? $" rot {r}" : "")}");
        if (op.Kind == "cells")
        {
            var c = op.Cells ?? [];
            int Min(int axis) => Enumerable.Range(0, c.Length / 3).Select(i => c[3 * i + axis]).DefaultIfEmpty(0).Min();
            int Max(int axis) => Enumerable.Range(0, c.Length / 3).Select(i => c[3 * i + axis]).DefaultIfEmpty(0).Max();
            sb.Append($" n {c.Length / 3} within x {Min(0)}..{Max(0)} y {Min(1)}..{Max(1)} z {Min(2)}..{Max(2)}");
            return sb.ToString();
        }
        static string P(int[]? p) => p is { Length: 3 } ? $"{p[0]},{p[1]},{p[2]}" : "?";
        sb.Append($" a {P(op.A)} b {P(op.B)}");
        if (op.Kind == "curve") sb.Append($" c {P(op.C)}");
        if (op.Kind == "cylinder") sb.Append($" axis {op.Axis ?? "z"}");
        if (op.Hollow) sb.Append($" hollow{(op.Closed ? " closed" : "")} thickness {op.Thickness}");
        return sb.ToString();
    }

    // Vue de dessus de chaque niveau à la couche base + 1 (murs, embrasures, fenêtres) : lettre du matériau, « . » vide.
    private static void AppendMaps(StringBuilder sb, PlanDocument doc)
    {
        var (w, d) = (doc.Grid.Width, doc.Grid.Depth);
        var zs = Enumerable.Range(0, doc.Levels.Count).Select(k => doc.LevelBaseY(k) + 1).Where(z => z < doc.Architecture.Height).ToList();
        if (zs.Count == 0 || doc.Architecture.Ops.Count == 0) return;
        if (w * d * zs.Count > MaxMapCells) { sb.AppendLine("Top views omitted: the plan is too large."); return; }

        var cells = new string?[w, d, zs.Count];
        var layer = zs.ToDictionary(z => z, z => zs.IndexOf(z));
        foreach (var op in doc.Architecture.Ops)
            ArchShapes.Paint(op, w, d, doc.Architecture.Height, (x, y, z) => { if (layer.TryGetValue(z, out var i)) cells[x, y, i] = op.Subtract ? null : op.Material; });
        var legend = new Dictionary<string, char>(StringComparer.Ordinal);
        foreach (var m in cells.Cast<string?>().Where(m => m is not null).GroupBy(m => m!).OrderByDescending(g => g.Count()).Select(g => g.Key))
            legend[m] = legend.Count < Letters.Length ? Letters[legend.Count] : '?';
        if (legend.Count == 0) return;

        sb.AppendLine("Top views, layer base + 1 of each level. Legend: " + string.Join(", ", legend.Select(l => $"{l.Value} {l.Key}")) + "; . = empty.");
        var tens = new string(Enumerable.Range(0, w).Select(x => x % 10 == 0 ? (char)('0' + x / 10 % 10) : ' ').ToArray());
        var units = new string(Enumerable.Range(0, w).Select(x => (char)('0' + x % 10)).ToArray());
        for (var i = 0; i < zs.Count; i++)
        {
            sb.AppendLine($"level {i}, z {zs[i]}:");
            sb.AppendLine("     " + tens);
            sb.AppendLine("     " + units);
            for (var y = 0; y < d; y++)
            {
                sb.Append($"{y,4} ");
                for (var x = 0; x < w; x++) sb.Append(cells[x, y, i] is { } m ? legend[m] : '.');
                sb.AppendLine();
            }
        }
    }
}
