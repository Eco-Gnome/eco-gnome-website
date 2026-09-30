using System.Text.Json;
using System.Text.Json.Serialization;

namespace ecocraft.BuildingPlanner.Generator;

// Programme architectural produit par le LLM (JSON camelCase, structured output sur BuildingProgramSchema) : sept
// tableaux homogènes, un par type d'élément. Coordonnées du plan : x vers l'est, y vers le sud, z vertical (z = 0 :
// couche du sol). Un volume est une pièce fermée : dalle à baseZ, murs sur `height` couches d'air, plafond au-dessus.
// Rotations et côtés : east = +x, south = +y, west = −x, north = −y. Le générateur re-clampe toutes les valeurs.
public sealed class BuildingProgram
{
    public string Name { get; set; } = "";
    public ProgramGrid Grid { get; set; } = new();
    public ProgramDefaults Defaults { get; set; } = new();
    public List<VolumeElement> Volumes { get; set; } = [];
    public List<SlabElement> Slabs { get; set; } = [];
    public List<RoofElement> Roofs { get; set; } = [];
    public List<PartitionElement> Partitions { get; set; } = [];
    public List<ColumnElement> Columns { get; set; } = [];
    public List<StairsElement> Stairs { get; set; } = [];
    public List<OpeningElement> Openings { get; set; } = [];
    public List<BlockElement> Blocks { get; set; } = [];
    public List<FenceElement> Fences { get; set; } = [];
    public List<FurnitureElement> Furniture { get; set; } = [];
}

public sealed class ProgramGrid
{
    public int Width { get; set; } = 30;
    public int Depth { get; set; } = 30;
}

// Matériau de repli : remplace tout matériau hors catalogue.
public sealed class ProgramDefaults
{
    public string WallMaterial { get; set; } = "";
}

// Boîte fermée : emprise x..x+width−1 × y..y+depth−1, dalle à baseZ (au sol aussi), murs sur baseZ+1..baseZ+height,
// plafond à baseZ+height+1. Le baseZ d'un volume définit un niveau du document. Sous un toit à deux ou quatre pans, pas de
// plafond : la pièce monte jusqu'au toit (BuildingGenerator.OpenTo), sauf flatCeiling (plafond plat et grenier à part).
public sealed class VolumeElement
{
    public string Id { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Depth { get; set; }
    public int BaseZ { get; set; }
    public int Height { get; set; } = 3;
    public string WallMaterial { get; set; } = "";
    public string WallForm { get; set; } = "";   // « » : forme du matériau (Wall, Wall_01 en FarEast, WallMid en Adobe) ; voir BuildingGenerator.WallForms
    public int WallThickness { get; set; } = 1;
    public string FloorMaterial { get; set; } = "";
    public string CeilingMaterial { get; set; } = "";
    public bool FlatCeiling { get; set; }
}

// Plateau plein (terrasse, balcon, dalle intermédiaire) de `thickness` couches à partir de z.
public sealed class SlabElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Depth { get; set; }
    public int Z { get; set; }
    public string Material { get; set; } = "";
    public int Thickness { get; set; } = 1;
}

// Toit posé sur l'emprise x..x+width−1 × y..y+depth−1 (élargie de `overhang` de chaque côté), première course à baseZ.
// style : flat (plateau + acrotère optionnel), shed (un pan, pente qui descend vers le sud pour ridgeAxis x, vers
// l'est pour y), gable (deux pans + pignons), hip (quatre pans). ridgeAxis : axe du faîte. Pente fixe de 45° (une course =
// une rangée et une couche). courses : 0 = jusqu'au faîte, N = auvent de N courses sans faîte ni pignons (pagode).
public sealed class RoofElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Depth { get; set; }
    public int BaseZ { get; set; }
    public string Style { get; set; } = "gable";
    public string RidgeAxis { get; set; } = "x";
    public int Courses { get; set; }
    public int Overhang { get; set; }
    public string Material { get; set; } = "";
    public bool Parapet { get; set; }
}

// Mur intérieur droit (axe dominant conservé) de (x0,y0) à (x1,y1) inclus, sur baseZ+1..baseZ+height.
public sealed class PartitionElement
{
    public string Id { get; set; } = "";
    public int X0 { get; set; }
    public int Y0 { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int BaseZ { get; set; }
    public int Height { get; set; } = 3;
    public string Material { get; set; } = "";
    public string WallForm { get; set; } = "";
    public int Thickness { get; set; } = 1;
}

// `count` poteaux de section size×size posés sur la dalle baseZ (baseZ+1..baseZ+height), à partir de (x,y) puis tous les
// (stepX, stepY). Le générateur les recale sur leur appui et le bloc qui les couvre (BuildingGenerator.ColumnSnap).
public sealed class ColumnElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int BaseZ { get; set; }
    public int Height { get; set; } = 3;
    public int Size { get; set; } = 1;
    public string Material { get; set; } = "";
    public int Count { get; set; } = 1;
    public int StepX { get; set; }
    public int StepY { get; set; }
}

// Escalier droit : première marche en (x,y) à fromZ+1, une marche par case dans `direction` jusqu'à toZ (dalle
// d'arrivée) ; la trémie est creusée dans la dalle à toZ. width = largeur perpendiculaire (vers la droite du sens de montée).
public sealed class StairsElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int FromZ { get; set; }
    public int ToZ { get; set; }
    public string Direction { get; set; } = "east";
    public int Width { get; set; } = 1;
    public string Material { get; set; } = "";
}

// Porte ou fenêtre dans un mur de volume ou de cloison (targetId), sur le côté `side`, à `offset` cases du début du mur
// (ouest ou nord), de `width` cases de large et `height` de haut, à `sill` couches au-dessus du sol (porte : 0).
// `door` : objet porte du catalogue posé dans l'embrasure (portes seulement) ; vide → porte de la famille du mur.
public sealed class OpeningElement
{
    public string TargetId { get; set; } = "";
    public string Side { get; set; } = "south";
    public int Offset { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 2;
    public int Sill { get; set; }
    public string Kind { get; set; } = "window";
    public string Door { get; set; } = "";
}

// Boîte d'une forme du jeu, posée telle quelle : le seul moyen d'atteindre les formes qu'aucun autre élément n'émet
// (quais, échelle, rondins empilés). Emprise x..x+width−1 × y..y+depth−1, de z à z+height−1.
public sealed class BlockElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1;
    public int Depth { get; set; } = 1;
    public int Z { get; set; }
    public int Height { get; set; } = 1;
    public string Form { get; set; } = "Cube";
    public int Rot { get; set; }
    public string Material { get; set; } = "";
}

// Clôture le long d'un tracé : segments droits de case en case entre points successifs (retour au premier si closed), posée
// sur la dalle baseZ (couches baseZ+1..baseZ+height). Le générateur choisit segments, angles, T, X et bouts d'après les
// raccords du tracé ; gates = cases du tracé laissées vides (portillon, passage).
public sealed class FenceElement
{
    public List<FenceCell> Points { get; set; } = [];
    public bool Closed { get; set; }
    public List<FenceCell> Gates { get; set; } = [];
    public int BaseZ { get; set; }
    public int Height { get; set; } = 1;
    public string Material { get; set; } = "";
}

public sealed class FenceCell
{
    public int X { get; set; }
    public int Y { get; set; }
}

// Meuble du catalogue posé au sol d'un niveau : (x, y) = case nord-ouest de son emprise une fois tourné de `rot` quarts
// de tour, baseZ = dalle du niveau (comme les cloisons). Le générateur en déduit la case d'ancrage du jeu.
public sealed class FurnitureElement
{
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int BaseZ { get; set; }
    public int Rot { get; set; }
}

public static class BuildingProgramJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static BuildingProgram Parse(string json)
        => JsonSerializer.Deserialize<BuildingProgram>(json, Options) ?? throw new JsonException("Empty building program.");
}
