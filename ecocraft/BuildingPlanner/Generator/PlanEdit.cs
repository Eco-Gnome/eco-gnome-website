using System.Text.Json;

namespace ecocraft.BuildingPlanner.Generator;

// Modification d'un plan existant produite par le LLM (outil submit_plan_edit, PlanEditSchema) : ops supprimées ou
// changées par id, creux, objets supprimés, puis éléments du programme ajoutés par le générateur. Appliquée par PlanEditor.
public sealed class PlanEdit
{
    public List<string> RemoveOps { get; set; } = [];
    public List<OpChange> ChangeOps { get; set; } = [];
    public List<CutElement> Cuts { get; set; } = [];
    public List<string> RemoveObjects { get; set; } = [];
    public BuildingProgram Add { get; set; } = new();   // name, grid et defaults ignorés : la grille est celle du plan
}

// Champs vides = inchangés : material « », form « », rot −1, a et b [] (ignorés sur une op cells).
public sealed class OpChange
{
    public string Id { get; set; } = "";
    public string Material { get; set; } = "";
    public string Form { get; set; } = "";
    public int Rot { get; set; } = -1;
    public int[] A { get; set; } = [];
    public int[] B { get; set; } = [];
}

// Boîte d'air creusée x..x+width−1 × y..y+depth−1, de z à z+height−1.
public sealed class CutElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Width { get; set; } = 1;
    public int Depth { get; set; } = 1;
    public int Height { get; set; } = 1;
}

public static class PlanEditJson
{
    public static PlanEdit Parse(string json)
        => JsonSerializer.Deserialize<PlanEdit>(json, BuildingProgramJson.Options) ?? throw new JsonException("Empty plan edit.");
}
