using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Applique une PlanEdit à une copie du plan : suppressions, changements d'ops, creux (ops subtract « cN »), puis les
// éléments ajoutés passent par le générateur amorcé avec le plan (ops ajoutées après les siennes). Un id inconnu, un
// matériau hors catalogue ou une forme inconnue laissent une note, jamais une erreur ; le document final est validé.
public static class PlanEditor
{
    public static GeneratorResult Apply(PlanDocument doc, PlanEdit edit, IReadOnlyCollection<string> allowedMaterials, IReadOnlyList<FurnitureInfo>? doorTypes = null, IReadOnlyCollection<FurnitureInfo>? furniture = null)
    {
        var work = PlanDocumentJson.Parse(PlanDocumentJson.Serialize(doc));
        var ops = work.Architecture.Ops;
        var notes = new List<string>();
        var allowed = allowedMaterials.ToHashSet(StringComparer.Ordinal);

        var remove = edit.RemoveOps.ToHashSet(StringComparer.Ordinal);
        foreach (var id in remove.Where(id => ops.All(o => o.Id != id))) notes.Add($"op « {id} » introuvable, pas supprimée");
        ops.RemoveAll(o => remove.Contains(o.Id));

        foreach (var id in edit.RemoveObjects)
            if (work.Levels.Sum(l => l.Objects.RemoveAll(o => o.Id == id)) == 0) notes.Add($"objet « {id} » introuvable, pas supprimé");

        foreach (var change in edit.ChangeOps)
        {
            var op = ops.FirstOrDefault(o => o.Id == change.Id);
            if (op is null) { notes.Add($"op « {change.Id} » introuvable, pas modifiée"); continue; }
            if (change.Material.Length > 0 && !op.Subtract)
            {
                if (allowed.Contains(change.Material)) op.Material = change.Material;
                else notes.Add($"op « {op.Id} » : matériau « {change.Material} » inconnu, gardé");
            }
            if (change.Form.Length > 0 && !op.Subtract)
            {
                if (BuildingGenerator.Forms.Contains(change.Form, StringComparer.Ordinal)) op.Form = change.Form;
                else notes.Add($"op « {op.Id} » : forme « {change.Form} » inconnue, gardée");
            }
            if (change.Rot >= 0 && !op.Subtract) op.Rot = change.Rot & 3;
            if (op.Kind != "cells" && change.A.Length == 3) op.A = change.A;
            if (op.Kind != "cells" && change.B.Length == 3) op.B = change.B;
        }

        var used = ops.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var n = 0;
        foreach (var c in edit.Cuts)
        {
            string id;
            do id = $"c{++n}"; while (!used.Add(id));
            ops.Add(new ArchOp
            {
                Id = id, Kind = "box", Subtract = true,
                A = [c.X, c.Y, c.Z], B = [c.X + Math.Max(1, c.Width) - 1, c.Y + Math.Max(1, c.Depth) - 1, c.Z + Math.Max(1, c.Height) - 1],
            });
        }

        var result = BuildingGenerator.Generate(edit.Add, allowedMaterials, doorTypes, furniture, work);
        return result with { Notes = [.. notes, .. result.Notes] };
    }
}
