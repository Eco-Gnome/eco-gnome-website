using System.Text.Json.Nodes;

namespace ecocraft.BuildingPlanner.Generator;

// JSON Schema de BuildingProgram, passé en outil non strict : additionalProperties:false partout, tous les champs requis,
// portes, meubles et formes = enums construites au runtime depuis le catalogue du serveur. Les matériaux du serveur sont
// listés une seule fois, dans la description de la racine, et les champs matériau sont de simples chaînes : l'outil n'étant
// pas strict, une enum ne contraindrait rien de plus, et la recopier dans chaque champ coûtait des milliers de tokens. Les bornes
// numériques sont dans les descriptions (minimum/maximum/maxItems ne sont pas supportés par les structured outputs) ;
// le générateur re-clampe de toute façon.
public static class BuildingProgramSchema
{
    public const int MaxGrid = 120;
    public const int MaxStoreyHeight = 12;
    public const int MaxItems = 40;

    // furniture : meubles proposés (nom + cellules d'occupancy, repère Eco) ; leurs emprises l×p×h sont listées dans la
    // description du tableau, le prompt système restant constant.
    // doors : portes proposées (nom + cellules), leur taille largeur×hauteur est listée dans la description du champ.
    public static JsonObject Build(IReadOnlyCollection<string> materials, IReadOnlyCollection<FurnitureInfo>? doors = null, IReadOnlyCollection<FurnitureInfo>? furniture = null)
    {
        if (materials.Count == 0) throw new ArgumentException("At least one material is required.", nameof(materials));
        var names = materials.Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToArray();

        JsonObject Material(string what) => Str($"{what} : nom technique d'un matériau de la liste en tête du programme.");
        var doorInfos = (doors ?? []).DistinctBy(d => d.Name).OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
        var doorWhat = "Porte seulement : objet porte du catalogue posé dans l'embrasure, assorti au style du bâtiment (en général la famille du mur, ex. HewnDoorItem pour HewnLogItem) ; « » pour une fenêtre."
            + " L'embrasure prend la taille de la porte (width et height ignorés) ; largeur×hauteur en cases : "
            + string.Join(", ", doorInfos.Select(d => $"{d.Name} {Math.Max(d.Size.W, d.Size.D)}×{d.Size.H}"));
        var door = doorInfos.Length > 0 ? Enum(doorWhat, doorInfos.Select(d => d.Name).Append("")) : Str(doorWhat);
        var pieces = (furniture ?? []).DistinctBy(f => f.Name).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        const string pieceWhat = "Objet du catalogue (nom technique, ex. HewnTableItem), de la famille des murs de sa pièce";
        var pieceType = pieces.Length > 0 ? Enum(pieceWhat, pieces.Select(f => f.Name)) : Str(pieceWhat);
        var footprints = pieces.Length > 0 ? " Emprises largeur×profondeur×hauteur en cases (rot 0) : " + string.Join(", ", pieces.Select(f => $"{f.Name} {f.Size.W}×{f.Size.D}×{f.Size.H}")) : "";
        JsonObject Rect(string what) => Obj(
            ("x", Int($"Colonne ouest de {what} (0 = bord ouest de la grille)")),
            ("y", Int($"Ligne nord de {what} (0 = bord nord de la grille)")),
            ("width", Int($"Largeur de {what} en cases (est-ouest), 3 à {MaxGrid}")),
            ("depth", Int($"Profondeur de {what} en cases (nord-sud), 3 à {MaxGrid}")));

        const string wallFormWhat = "Forme des murs : « » = celle du matériau (Wall, Wall_01 en FarEastLumberItem, WallMid en AdobeItem) ; Wall pour tous les matériaux sauf FarEastLumberItem et AdobeItem ; Wall_01..Wall_19 (murs FarEast à colombages) pour FarEastLumberItem seulement ; WallMid pour AdobeItem seulement";
        JsonObject WallForm() => Enum(wallFormWhat, BuildingGenerator.WallForms.Prepend(""));
        var volume = Merge(Rect("l'emprise du volume, murs compris"), Obj(
            ("id", Str("Identifiant unique, cible des ouvertures (ex. « v1 »)")),
            ("baseZ", Int("Couche de la dalle : 0 au sol, sinon le plafond du volume du dessous (baseZ + height + 1 de celui-ci)")),
            ("height", Int($"Couches d'air entre dalle et plafond, 2 à {MaxStoreyHeight}")),
            ("wallMaterial", Material("Matériau des murs")),
            ("wallForm", WallForm()),
            ("wallThickness", Int("Épaisseur des murs en cases, 1 à 3 (murs FarEast : toujours 1)")),
            ("floorMaterial", Material("Matériau de la dalle")),
            ("ceilingMaterial", Material("Matériau du plafond")),
            ("flatCeiling", Bool("true seulement si la demande veut explicitement un plafond plat (grenier) sous un toit à deux ou quatre pans ; false : la pièce monte jusqu'au toit"))));

        var slab = Merge(Rect("le plateau"), Obj(
            ("z", Int("Couche de la face inférieure du plateau")),
            ("material", Material("Matériau du plateau")),
            ("thickness", Int("Épaisseur en couches, 1 à 3"))));

        var roof = Merge(Rect("le toit (avant débord)"), Obj(
            ("baseZ", Int("Couche de la première course, en général le plafond du volume couvert + 1")),
            ("style", Enum("flat : plateau ; shed : un pan (descend vers le sud pour ridgeAxis x, vers l'est pour y) ; gable : deux pans et pignons ; hip : quatre pans", ["flat", "shed", "gable", "hip"])),
            ("ridgeAxis", Enum("Axe du faîte : x (faîte est-ouest, pentes nord et sud) ou y", ["x", "y"])),
            ("courses", Int($"Pente fixe de 45°, une rangée par couche. 0 = toit complet jusqu'au faîte ; 1 à {BuildingGenerator.MaxCourses} = auvent de ce nombre de courses depuis le bord, sans faîte ni pignons, autour de l'étage posé dessus (pagode : débord + retrait de l'étage du dessus)")),
            ("overhang", Int("Débord au-delà de l'emprise, en cases, 0 à 2")),
            ("material", Material("Matériau du toit")),
            ("parapet", Bool("flat seulement : acrotère d'une case sur le pourtour"))));

        var partition = Obj(
            ("id", Str("Identifiant unique, cible des ouvertures (ex. « p1 »)")),
            ("x0", Int("Début du mur (x)")), ("y0", Int("Début du mur (y)")),
            ("x1", Int("Fin du mur (x) ; le mur est droit : même x ou même y que le début")), ("y1", Int("Fin du mur (y)")),
            ("baseZ", Int("Couche de la dalle du niveau (le baseZ du volume qui contient la cloison)")),
            ("height", Int($"Couches d'air, 1 à {MaxStoreyHeight}")),
            ("material", Material("Matériau de la cloison")),
            ("wallForm", WallForm()),
            ("thickness", Int("Épaisseur en cases, 1 à 3 (murs FarEast : toujours 1)")));

        var column = Obj(
            ("x", Int("Colonne du premier poteau")), ("y", Int("Ligne du premier poteau")),
            ("baseZ", Int("Couche du sol ou de la dalle qui porte le poteau (0 au sol) ; le poteau occupe baseZ+1 à baseZ+height")),
            ("height", Int("Hauteur en couches, 1 à 40 ; sous un toit ou une dalle, jusqu'à la couche sous celui-ci (le générateur comble un écart de deux couches au plus)")),
            ("size", Int("Section en cases, 1 à 3")),
            ("material", Material("Matériau des poteaux")),
            ("count", Int("Nombre de poteaux alignés, 1 à 40")),
            ("stepX", Int("Pas entre poteaux en x (0 si alignés en y)")), ("stepY", Int("Pas entre poteaux en y")));

        var stairs = Obj(
            ("x", Int("Colonne de la première marche")), ("y", Int("Ligne de la première marche")),
            ("fromZ", Int("Couche de la dalle de départ (baseZ du niveau bas)")),
            ("toZ", Int("Couche de la dalle d'arrivée (baseZ du niveau haut) ; une marche par couche")),
            ("direction", Enum("Sens de montée", ["east", "south", "west", "north"])),
            ("width", Int("Largeur en cases, 1 à 4")),
            ("material", Material("Matériau des marches")));

        var block = Merge(Rect("la boîte de blocs"), Obj(
            ("z", Int("Couche de la base de la boîte")),
            ("height", Int("Hauteur en couches, 1 à 40")),
            ("form", Enum("Forme du jeu posée dans toutes les cellules de la boîte", BuildingGenerator.Forms)),
            ("rot", Int("Quart de tour du bloc, 0 à 3 : la face avant regarde est, nord, ouest, sud")),
            ("material", Material("Matériau des blocs"))));

        JsonObject Cell(string what) => Obj(("x", Int($"Colonne de {what}")), ("y", Int($"Ligne de {what}")));
        var fence = Obj(
            ("points", Arr("Tracé : cases successives reliées en ligne droite (même x ou même y), angles compris", Cell("la case"))),
            ("closed", Bool("true : le tracé revient au premier point (enclos) ; laisser alors au moins un portillon")),
            ("gates", Arr("Cases du tracé laissées vides : portillon, passage d'une allée", Cell("la case vide"))),
            ("baseZ", Int("Couche du sol ou de la dalle qui porte la clôture (0 au sol) ; la clôture occupe baseZ+1 à baseZ+height")),
            ("height", Int("Hauteur en couches, 1 à 4 (en général 1)")),
            ("material", Material("Matériau : FarEastLumberItem, AdobeItem, une bûche équarrie (HewnLogItem : garde-corps de ponton), CorrugatedSteelItem, ReinforcedConcreteItem, FlatSteelItem, un Composite* ou un Ashlar*")));

        var opening = Obj(
            ("targetId", Str("id du volume ou de la cloison percé")),
            ("side", Enum("Mur percé du volume (pour une cloison : ignoré)", ["north", "east", "south", "west"])),
            ("offset", Int("Distance en cases depuis l'angle ouest (murs nord/sud) ou nord (murs est/ouest), jamais dans l'angle : ≥ 1")),
            ("width", Int("Largeur en cases, 1 à 6")),
            ("height", Int("Hauteur en couches, 1 à height du volume")),
            ("sill", Int("Couches au-dessus du sol : 0 pour une porte, ≥ 1 pour une fenêtre")),
            ("kind", Enum("door : au sol, franchissable ; window : appui ≥ 1", ["door", "window"])),
            ("door", door));

        var piece = Obj(
            ("type", pieceType),
            ("x", Int("Colonne ouest de l'emprise du meuble (0 = bord ouest de la grille)")),
            ("y", Int("Ligne nord de l'emprise du meuble")),
            ("baseZ", Int("Couche de la dalle du niveau qui le porte (baseZ du volume qui le contient)")),
            ("rot", Int("Quart de tour, 0 à 3 ; pour 1 et 3 l'emprise largeur×profondeur devient profondeur×largeur")));

        var root = Obj(
            ("name", Str("Nom court du bâtiment")),
            ("grid", Obj(("width", Int($"Largeur de la grille en cases, 8 à {MaxGrid}")), ("depth", Int($"Profondeur de la grille en cases, 8 à {MaxGrid}")))),
            ("defaults", Obj(("wallMaterial", Material("Matériau de repli")))),
            ("volumes", Arr("Pièces fermées (dalle + murs + plafond), un volume par pièce ou par étage", volume)),
            ("slabs", Arr("Plateaux pleins : terrasses, balcons", slab)),
            ("roofs", Arr("Toits", roof)),
            ("partitions", Arr("Murs intérieurs", partition)),
            ("columns", Arr("Poteaux (pilotis, colonnades)", column)),
            ("stairs", Arr("Escaliers entre deux niveaux", stairs)),
            ("openings", Arr("Portes et fenêtres, déclarées par cible et côté", opening)),
            ("fences", Arr("Clôtures, barrières, garde-corps et rambardes, tracées case par case ; le générateur pose segments, angles, T, X et bouts", fence)),
            ("blocks", Arr($"Boîtes d'une forme donnée : quais, échelles, rondins empilés — tout ce que les autres éléments ne savent pas décrire (au plus {BuildingGenerator.MaxBlocks})", block)),
            ("furniture", Arr("Meubles posés au sol des pièces, entièrement à l'intérieur des murs, jamais devant une porte." + footprints, piece)));
        root["description"] = "Matériaux du serveur (seuls noms admis dans les champs matériau) : " + string.Join(", ", names);
        return root;
    }

    // Schéma de PlanEdit : modification d'un plan existant. « add » reprend les éléments du programme (sans name, grid ni
    // defaults : la grille est celle du plan), la liste des matériaux passe à la racine.
    public static JsonObject BuildEdit(IReadOnlyCollection<string> materials, IReadOnlyCollection<FurnitureInfo>? doors = null, IReadOnlyCollection<FurnitureInfo>? furniture = null)
    {
        var add = Build(materials, doors, furniture);
        var description = add["description"]!.GetValue<string>();
        add.Remove("description");
        var props = (JsonObject)add["properties"]!;
        var required = (JsonArray)add["required"]!;
        foreach (var name in new[] { "name", "grid", "defaults" })
        {
            props.Remove(name);
            required.Remove(required.First(r => r!.GetValue<string>() == name));
        }
        add["description"] = "Éléments ajoutés au plan, mêmes règles qu'un nouveau bâtiment, dans les coordonnées du plan ; posés après les ops existantes. openings.targetId peut viser l'id d'une op de mur existante";

        JsonObject Corner(string what) => new() { ["type"] = "array", ["description"] = $"{what} [x, y, z] ; [] = inchangé (ignoré sur une op cells)", ["items"] = new JsonObject { ["type"] = "integer" } };
        var change = Obj(
            ("id", Str("id de l'op à modifier")),
            ("material", Str("Nouveau matériau, de la liste en tête ; « » = inchangé")),
            ("form", Enum("Nouvelle forme ; « » = inchangée", BuildingGenerator.Forms.Prepend(""))),
            ("rot", Int("Nouveau quart de tour 0 à 3 ; −1 = inchangé")),
            ("a", Corner("Nouveau premier coin")),
            ("b", Corner("Nouveau second coin")));
        var cut = Obj(
            ("x", Int("Colonne ouest du creux")), ("y", Int("Ligne nord du creux")), ("z", Int("Couche du bas du creux")),
            ("width", Int("Largeur en cases (est-ouest)")), ("depth", Int("Profondeur en cases (nord-sud)")), ("height", Int("Hauteur en couches")));
        // Sans plafond d'éléments : repeindre tout un bâtiment touche vite plus de quarante ops.
        JsonObject Many(string description, JsonObject items) => new() { ["type"] = "array", ["description"] = description, ["items"] = items };
        JsonObject Ids(string description) => Many(description, new JsonObject { ["type"] = "string" });
        var root = Obj(
            ("removeOps", Ids("ids des ops supprimées")),
            ("changeOps", Many("Ops repeintes, changées de forme ou redimensionnées", change)),
            ("cuts", Many("Boîtes d'air creusées après les ops existantes (passage, trémie, embrasure dans un mur au crayon)", cut)),
            ("removeObjects", Ids("ids des objets supprimés (portes, meubles) ; pour déplacer un meuble, le supprimer et le rajouter dans add.furniture")),
            ("add", add));
        root["description"] = description;
        return root;
    }

    private static JsonObject Obj(params (string Name, JsonObject Schema)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema) in properties) { props[name] = schema; required.Add(name); }
        return new JsonObject { ["type"] = "object", ["properties"] = props, ["required"] = required, ["additionalProperties"] = false };
    }

    private static JsonObject Merge(JsonObject a, JsonObject b)
    {
        var props = (JsonObject)a["properties"]!;
        var required = (JsonArray)a["required"]!;
        foreach (var (name, schema) in (JsonObject)b["properties"]!) { props[name] = schema!.DeepClone(); required.Add(name); }
        return a;
    }

    private static JsonObject Arr(string description, JsonObject items) => new() { ["type"] = "array", ["description"] = $"{description} (au plus {MaxItems})", ["items"] = items };
    private static JsonObject Int(string description) => new() { ["type"] = "integer", ["description"] = description };
    private static JsonObject Str(string description) => new() { ["type"] = "string", ["description"] = description };
    private static JsonObject Bool(string description) => new() { ["type"] = "boolean", ["description"] = description };
    private static JsonObject Enum(string description, IEnumerable<string> values) => new() { ["type"] = "string", ["description"] = description, ["enum"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray()) };
}
