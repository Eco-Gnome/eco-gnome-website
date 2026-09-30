using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Volumes, niveaux, plateaux, cloisons, poteaux, escaliers, ouvertures.
public static partial class BuildingGenerator
{
    private sealed partial class Build
    {
        public List<Volume> NormalizeVolumes()
        {
            var result = new List<Volume>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var v in Take(P.Volumes, "volumes"))
            {
                var what = $"volume « {v.Id} »";
                var id = ids.Add(v.Id) ? v.Id : "";
                if (id.Length == 0 && v.Id.Length > 0) Notes.Add($"{what} : identifiant en double, les ouvertures visent le premier");
                var r = ClampRect(v.X, v.Y, v.Width, v.Depth, 3, what);
                var wall = Material(v.WallMaterial, what);
                var form = WallFormFor(v.WallForm, wall, what);
                var thickness = form == "Wall" ? Math.Clamp(v.WallThickness, 1, Math.Min(3, (Math.Min(r.X1 - r.X0, r.Y1 - r.Y0)) / 2)) : 1;   // murs tournés : une case
                var baseZ = Math.Clamp(v.BaseZ, 0, MaxBaseZ);
                var height = Math.Clamp(v.Height, 1, MaxStoreyHeight);
                if (baseZ != v.BaseZ || height != v.Height) Notes.Add($"{what} : hauteur ramenée à baseZ {baseZ}, {height} couches");
                result.Add(new Volume(id, r, baseZ, height, thickness, wall, form, Material(v.FloorMaterial, what), Material(v.CeilingMaterial, what), v.FlatCeiling));
            }
            return result;
        }

        public List<Partition> NormalizePartitions()
        {
            var result = new List<Partition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in Take(P.Partitions, "cloisons"))
            {
                var what = $"cloison « {p.Id} »";
                var id = ids.Add(p.Id) ? p.Id : "";
                var alongX = Math.Abs(p.X1 - p.X0) >= Math.Abs(p.Y1 - p.Y0);
                var material = Material(p.Material, what);
                var form = WallFormFor(p.WallForm, material, what);
                var t = form == "Wall" ? Math.Clamp(p.Thickness, 1, 3) : 1;
                var r = alongX
                    ? ClampRect(Math.Min(p.X0, p.X1), p.Y0, Math.Abs(p.X1 - p.X0) + 1, t, 1, what)
                    : ClampRect(p.X0, Math.Min(p.Y0, p.Y1), t, Math.Abs(p.Y1 - p.Y0) + 1, 1, what);
                result.Add(new Partition(id, r, Math.Clamp(p.BaseZ, 0, MaxBaseZ), Math.Clamp(p.Height, 1, MaxStoreyHeight), material, form, alongX));
            }
            return result;
        }

        // Pile de niveaux : une dalle par baseZ distinct de volume (0 toujours), deux dalles séparées d'au moins deux
        // couches, niveaux vides intercalés si un écart dépasse la hauteur maximale. Invariant : Doc.LevelBaseY(k) == Bases[k].
        public void BuildLevels(List<Volume> volumes)
        {
            if (Seeded) { ExtendLevels(volumes); return; }
            var bases = new List<int>();
            foreach (var b in volumes.Select(v => v.BaseZ).Append(0).Distinct().Order())
            {
                if (bases.Count > 0 && b - bases[^1] < 2) { Notes.Add($"dalle à z = {b} trop proche de z = {bases[^1]} : pas de niveau séparé"); continue; }
                while (bases.Count > 0 && b - bases[^1] > PlanValidator.MaxHeight + 1) bases.Add(bases[^1] + PlanValidator.MaxHeight + 1);
                bases.Add(b);
            }
            if (bases.Count > PlanValidator.MaxLevels) { Notes.Add($"plus de {PlanValidator.MaxLevels} niveaux : les plus hauts n'ont pas de niveau"); bases = bases.Take(PlanValidator.MaxLevels).ToList(); }
            Bases = bases;

            for (var k = 0; k < bases.Count; k++)
            {
                var height = k + 1 < bases.Count
                    ? bases[k + 1] - bases[k] - 1
                    : Math.Clamp(volumes.Where(v => v.BaseZ == bases[k]).Select(v => v.Height).DefaultIfEmpty(Doc.Defaults.WallHeight).Max(), 1, PlanValidator.MaxArchitectureHeight - bases[k] - 1);
                Doc.Levels.Add(new PlanLevel { Height = height });
            }
            Doc.Defaults.WallHeight = Math.Clamp(volumes.Where(v => v.BaseZ == 0).Select(v => v.Height).DefaultIfEmpty(3).Max(), 1, PlanValidator.MaxHeight);
        }

        // Plan existant : ses niveaux restent tels quels ; un volume posé au-dessus du dernier en ajoute un (la tranche du
        // dernier s'arrête sous sa dalle), un volume à une autre hauteur est construit sans niveau à lui.
        private void ExtendLevels(List<Volume> volumes)
        {
            Bases = Enumerable.Range(0, Doc.Levels.Count).Select(Doc.LevelBaseY).ToList();
            foreach (var b in volumes.Select(v => v.BaseZ).Distinct().Order())
            {
                if (Bases.Contains(b)) continue;
                if (b - Bases[^1] < 2) { Notes.Add($"volume à z = {b} : pas un niveau du plan, aucun meuble posé dessus"); continue; }
                while (Doc.Levels.Count < PlanValidator.MaxLevels && b - Bases[^1] > PlanValidator.MaxHeight + 1)
                {
                    Doc.Levels[^1].Height = PlanValidator.MaxHeight;
                    Doc.Levels.Add(new PlanLevel());
                    Bases.Add(Bases[^1] + PlanValidator.MaxHeight + 1);
                }
                if (Doc.Levels.Count >= PlanValidator.MaxLevels) { Notes.Add($"plus de {PlanValidator.MaxLevels} niveaux : le volume à z = {b} n'a pas de niveau"); continue; }
                Doc.Levels[^1].Height = b - Bases[^1] - 1;
                Doc.Levels.Add(new PlanLevel { Height = volumes.Where(v => v.BaseZ == b).Max(v => v.Height) });
                Bases.Add(b);
            }
        }

        // Murs du plan existant, cibles des ouvertures et appui du mobilier : une boîte creuse = volume (id de l'op ; boîte
        // fermée de l'outil Pièce : dalle et plafond compris), une boîte pleine d'au plus trois cases d'épaisseur = cloison.
        public (List<Volume> Walls, List<Partition> Partitions) ExistingWalls()
        {
            var walls = new List<Volume>();
            var partitions = new List<Partition>();
            if (!Seeded) return (walls, partitions);
            foreach (var op in Doc.Architecture.Ops.Where(o => o.Kind == "box" && !o.Subtract && o.Material is not null && o.A is { Length: 3 } && o.B is { Length: 3 }))
            {
                var r = new Rect(Math.Min(op.A![0], op.B![0]), Math.Min(op.A[1], op.B[1]), Math.Max(op.A[0], op.B[0]), Math.Max(op.A[1], op.B[1]));
                int z0 = Math.Min(op.A[2], op.B[2]), z1 = Math.Max(op.A[2], op.B[2]);
                var form = op.Form ?? "Wall";
                if (op.Hollow && r.X1 - r.X0 >= 2 && r.Y1 - r.Y0 >= 2)
                {
                    var (baseZ, height) = op.Closed ? (z0, z1 - z0 - 1) : (z0 - 1, z1 - z0 + 1);
                    if (height >= 1) walls.Add(new Volume(op.Id, r, baseZ, height, Math.Max(1, op.Thickness), op.Material!, form, op.Material!, op.Material!, false));
                }
                else if (!op.Hollow && Math.Min(r.X1 - r.X0, r.Y1 - r.Y0) < 3 && Math.Max(r.X1 - r.X0, r.Y1 - r.Y0) >= 2 && z1 > z0)
                    partitions.Add(new Partition(op.Id, r, z0 - 1, z1 - z0 + 1, op.Material!, form, r.X1 - r.X0 >= r.Y1 - r.Y0));
            }
            return (walls, partitions);
        }

        public int LevelIndexOf(int baseZ) => Bases.IndexOf(baseZ);

        // Murs (boîte creuse, ou anneau de murs tournés), dalle (au sol aussi : elle remplace le terrain), plafond, sauf sous un
        // toit qui ferme la pièce (OpenTo).
        public void EmitVolumes(List<Volume> volumes, List<Roof> roofs)
        {
            EmitCategory("volumes", volumes.Select(v =>
            {
                var ops = v.WallForm == "Wall" ? [Box(v.R, v.BaseZ + 1, v.BaseZ + v.Height, v.Wall, "Wall", hollow: v.Thickness)] : FramedRing(v);
                ops.Add(Box(v.R, v.BaseZ, v.BaseZ, v.Floor, "Floor"));
                if (!roofs.Any(r => OpenTo(v, r, volumes))) ops.Add(Box(v.R, v.BaseZ + v.Height + 1, v.BaseZ + v.Height + 1, v.Ceiling, "Floor"));
                return ops;
            }));
        }

        public void EmitSlabs()
        {
            EmitCategory("plateaux", Take(P.Slabs, "plateaux").Select((s, i) =>
            {
                var r = ClampRect(s.X, s.Y, s.Width, s.Depth, 1, $"plateau n° {i + 1}");
                var z = Math.Clamp(s.Z, 0, MaxBaseZ);
                return new List<ArchOp> { Box(r, z, z + Math.Clamp(s.Thickness, 1, 3) - 1, Material(s.Material, $"plateau n° {i + 1}"), "Floor") };
            }));
        }

        public void EmitPartitions(List<Partition> partitions, List<Volume> volumes)
        {
            EmitCategory("cloisons", partitions.Select(p => new List<ArchOp> { Box(p.R, p.BaseZ + 1, p.BaseZ + p.Height, p.Material, p.WallForm, p.WallForm == "Wall" ? 0 : p.AlongX ? 0 : 1) }));
            EmitCategory("jonctions", Junctions(partitions, volumes));
        }

        // Poteaux : baseZ = dalle ou sol qui les porte (comme clôtures et cloisons), fût sur baseZ+1..baseZ+height. Le fût ne
        // remplace jamais un bloc déjà posé (dalle, mur, toit : il le traverse) et, à ColumnSnap couches près, descend sur son
        // appui et monte jusqu'au bloc du dessus (programme décalé d'une couche, toit posé au plafond ou juste au-dessus).
        public void EmitColumns()
        {
            var columns = Take(P.Columns, "poteaux");
            if (columns.Count == 0) return;
            var h = Math.Min(PlanValidator.MaxArchitectureHeight, Doc.Architecture.Ops.Select(ArchShapes.MaxZ).DefaultIfEmpty(0).Max() + 1);
            Doc.Architecture.Height = h;   // recalculée à la fin de Generate
            var built = ArchLayer.Evaluate(Doc, h);
            EmitCategory("poteaux", columns.Select((c, i) =>
            {
                var what = $"poteaux n° {i + 1}";
                var size = Math.Clamp(c.Size, 1, 3);
                var baseZ = Math.Clamp(c.BaseZ, 0, MaxBaseZ);
                var material = Material(c.Material, what);
                var ops = new List<ArchOp>();
                for (var n = 0; n < Math.Clamp(c.Count, 1, MaxItems); n++)
                {
                    int x = c.X + n * c.StepX, y = c.Y + n * c.StepY;
                    if (x + size - 1 < 0 || y + size - 1 < 0 || x >= W || y >= D) { Notes.Add($"{what} : poteau hors grille ignoré"); continue; }
                    // Couche occupée sous l'emprise : terrain (z = 0) ou bloc déjà posé.
                    bool Solid(int z) => z <= 0 || Enumerable.Range(x, size).Any(cx => Enumerable.Range(y, size).Any(cy => built.IsSolid(cx, cy, z)));
                    int bottom = baseZ + 1, top = baseZ + Math.Clamp(c.Height, 1, 40);
                    var below = Enumerable.Range(0, ColumnSnap + 1).FirstOrDefault(g => Solid(bottom - 1 - g), 0);
                    var above = Enumerable.Range(0, ColumnSnap + 1).FirstOrDefault(g => Solid(top + 1 + g), 0);
                    bottom -= below;
                    top += above;
                    int start = -1, runs = ops.Count;
                    for (var z = bottom; z <= top + 1; z++)
                    {
                        var free = z <= top && !Solid(z);
                        if (free && start < 0) start = z;
                        if (!free && start >= 0) { ops.Add(Box(x, y, start, x + size - 1, y + size - 1, z - 1, material, "Column")); start = -1; }
                    }
                    if (ops.Count == runs) Notes.Add($"{what} : poteau entièrement dans des blocs déjà posés, ignoré");
                }
                return ops;
            }));
        }

        // Une marche par couche de fromZ+1 à toZ (op cells), trémie creusée dans la dalle d'arrivée avant les marches.
        public void EmitStairs()
        {
            EmitCategory("escaliers", Take(P.Stairs, "escaliers").Select((s, i) =>
            {
                var what = $"escalier n° {i + 1}";
                var steps = s.ToZ - s.FromZ;
                if (steps < 1 || s.FromZ < 0 || s.ToZ > MaxBaseZ) { Notes.Add($"{what} : hauteur invalide, ignoré"); return new List<ArchOp>(); }
                var r = DirIndex(s.Direction);
                var (dx, dy) = Dir[r];
                var (lx, ly) = Dir[(r + 1) % 4];
                var width = Math.Clamp(s.Width, 1, 4);
                var cells = new List<int>();
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                for (var k = 0; k < steps; k++)
                for (var j = 0; j < width; j++)
                {
                    int x = s.X + k * dx + j * lx, y = s.Y + k * dy + j * ly;
                    cells.AddRange([x, y, s.FromZ + 1 + k]);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                // Les marches montent vers dir(r) : le côté bas du bloc regarde dir(r+2) (StairsMid FarEast et Adobe : même convention).
                var material = Material(s.Material, what);
                return new List<ArchOp> { Cut(minX, minY, s.ToZ, maxX, maxY, s.ToZ), Cells(cells, material, IsFarEast(material) || IsAdobe(material) ? "StairsMid" : material == "FlatSteelItem" ? "FloatStairs" : "Stairs", LowToward((r + 2) % 4)) };
            }));
        }

        // Boîtes de forme libre : une op par entrée, forme inconnue remplacée par Cube. Le rendu choisit lui-même la
        // variante du mesh d'après les voisins, donc une simple rangée suffit pour une barrière ou un tablier.
        public void EmitBlocks()
        {
            EmitCategory("blocs", Take(P.Blocks, "blocs", MaxBlocks).Select((bl, i) =>
            {
                var what = $"bloc n° {i + 1}";
                var form = bl.Form;
                if (!Forms.Contains(form, StringComparer.Ordinal)) { Notes.Add($"{what} : forme « {form} » inconnue, remplacée par Cube"); form = "Cube"; }
                var r = ClampRect(bl.X, bl.Y, bl.Width, bl.Depth, 1, what);
                var z = Math.Clamp(bl.Z, 0, MaxBaseZ);
                var top = Math.Min(z + Math.Clamp(bl.Height, 1, 40) - 1, PlanValidator.MaxArchitectureHeight - 1);
                return new List<ArchOp> { Box(r, z, top, Material(bl.Material, what), form, Math.Clamp(bl.Rot, 0, 3)) };
            }));
        }

        // Ouvertures : jamais dans un angle (x ∈ [x0+1, x1−1]), linteau sous le plafond. Une fenêtre n'est pas creusée :
        // ses cellules sont repeintes en WindowGrilles (bloc plein en jeu, coût inchangé, pièce fermée en 2D et en 3D).
        // Une porte est creusée à la taille de l'objet porte (BuildDoor) : le creux ouvre la pièce, l'objet porte (cellules
        // Wall) la referme, comme en jeu. Fenêtres émises avant les portes : sur des cellules communes, le creux de la
        // porte l'emporte.
        public List<ArchOp> BuildOpenings(List<Volume> volumes, List<Partition> partitions)
        {
            var windows = new List<ArchOp>();
            var doors = new List<ArchOp>();
            foreach (var o in Take(P.Openings, "ouvertures"))
            {
                var what = $"ouverture sur « {o.TargetId} »";
                Rect wall; int baseZ, height, rotation; string material; bool alongX;
                var volume = volumes.FirstOrDefault(v => v.Id.Length > 0 && v.Id == o.TargetId);
                if (volume is not null)
                {
                    var r = volume.R;
                    var t = volume.Thickness;
                    (wall, rotation) = o.Side switch
                    {
                        "north" => (new Rect(r.X0, r.Y0, r.X1, r.Y0 + t - 1), 0),
                        "west" => (new Rect(r.X0, r.Y0, r.X0 + t - 1, r.Y1), 3),
                        "east" => (new Rect(r.X1 - t + 1, r.Y0, r.X1, r.Y1), 1),
                        _ => (new Rect(r.X0, r.Y1 - t + 1, r.X1, r.Y1), 2),
                    };
                    alongX = o.Side is not ("west" or "east");
                    (baseZ, height, material) = (volume.BaseZ, volume.Height, volume.Wall);
                }
                else if (partitions.FirstOrDefault(p => p.Id.Length > 0 && p.Id == o.TargetId) is { } partition)
                {
                    (wall, alongX, baseZ, height, material) = (partition.R, partition.AlongX, partition.BaseZ, partition.Height, partition.Material);
                    rotation = alongX ? 0 : 1;
                }
                else { Notes.Add($"{what} : cible introuvable, ignorée"); continue; }

                if (o.Kind == "door") { doors.Add(BuildDoor(o, what, wall, alongX, baseZ, height, rotation, material)); continue; }
                var sill = Math.Max(1, o.Sill);
                var z0 = baseZ + 1 + sill;
                var z1 = Math.Min(z0 + Math.Max(1, o.Height) - 1, baseZ + height);
                int a0 = alongX ? wall.X0 : wall.Y0, a1 = alongX ? wall.X1 : wall.Y1;
                var s0 = Math.Clamp(a0 + Math.Max(1, o.Offset), a0 + 1, a1 - 1);
                var s1 = Math.Clamp(s0 + Math.Max(1, o.Width) - 1, s0, a1 - 1);
                if (z1 < z0 || a1 - a0 < 2) { Notes.Add($"{what} : ne tient pas dans le mur, ignorée"); continue; }
                if (s0 != a0 + o.Offset || s1 - s0 + 1 != o.Width || z1 - z0 + 1 != o.Height || sill != o.Sill) Notes.Add($"{what} : ajustée pour rester dans le mur");

                int x0 = alongX ? s0 : wall.X0, x1 = alongX ? s1 : wall.X1, y0 = alongX ? wall.Y0 : s0, y1 = alongX ? wall.Y1 : s1;
                // Rot : sets dont la fenêtre est posée tournée par le joueur (Brick) ; la vitre de base est dans le plan y-z,
                // un quart de tour pour un mur le long de x. Ignoré par les sets qui orientent la fenêtre par ses voisins.
                // FarEast et Adobe : fenêtre Window, le long de x en rot 0 ; acier et béton : Window aussi, orientée par ses voisins.
                windows.Add(UsesWindowForm(material) ? Box(x0, y0, z0, x1, y1, z1, material, "Window", alongX ? 0 : 1) : Box(x0, y0, z0, x1, y1, z1, material, "WindowGrilles", alongX ? 1 : 0));
            }
            return windows.Concat(doors).ToList();
        }

        // Une porte par embrasure, à sa vraie taille : cellules du catalogue tournées. Rotation de base = côté du mur
        // (nord 0, est 1, sud 2, ouest 3) ; une porte dont le plan est en z au rot 0 (grandes portes) prend un quart de tour
        // de plus (sens intérieur/extérieur non vérifié en jeu). Porte trop grande pour le mur ou l'étage : la porte de la
        // famille du mur, puis la première qui tient ; aucune → embrasure 1×2 vide.
        private ArchOp BuildDoor(OpeningElement o, string what, Rect wall, bool alongX, int baseZ, int height, int rotation, string material)
        {
            int a0 = alongX ? wall.X0 : wall.Y0, a1 = alongX ? wall.X1 : wall.Y1;
            var requested = DoorFor(o.Door, material, what);
            var family = DoorFamily(material);
            List<FurnitureInfo> candidates = requested is null ? [] : DoorInfos.OrderBy(d => d.Name == requested ? 0 : d.Name == family ? 1 : 2).ToList();
            foreach (var info in candidates)
            foreach (var rot in new[] { rotation, (rotation + 1) % 4 })
            {
                var cells = (info.Cells.Count > 0 ? info.Cells : [Vec3i.Zero]).Select(c => Geometry.Rotate(c, rot)).ToList();
                int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X), minZ = cells.Min(c => c.Z), maxZ = cells.Max(c => c.Z), minY = cells.Min(c => c.Y);
                if ((alongX ? maxZ - minZ : maxX - minX) != 0) continue;   // pas dans le plan du mur à cette rotation
                var w = (alongX ? maxX - minX : maxZ - minZ) + 1;
                var h = cells.Max(c => c.Y) - minY + 1;
                if (w > a1 - a0 - 1 || h > height) break;                  // trop grande : porte suivante
                var s0 = Math.Clamp(a0 + Math.Max(1, o.Offset), a0 + 1, a1 - w);
                if (info.Name != requested) Notes.Add($"{what} : porte {requested} trop grande pour ce mur, remplacée par {info.Name}");
                if (s0 != a0 + o.Offset || w != o.Width) Notes.Add($"{what} : embrasure ramenée à la porte {info.Name} ({w}×{h})");
                return PlaceDoor(what, wall, alongX, baseZ, s0, w, h, new PlanObject { Type = info.Name, Z = 1 - minY, Rotation = rot }, minX, minZ);
            }
            Notes.Add(requested is null ? $"{what} : aucune porte au catalogue, embrasure vide" : $"{what} : aucune porte ne tient dans ce mur, embrasure vide");
            return PlaceDoor(what, wall, alongX, baseZ, Math.Clamp(a0 + Math.Max(1, o.Offset), a0 + 1, a1 - 1), 1, Math.Min(2, height), null, 0, 0);
        }

        // Creuse l'embrasure sur toute l'épaisseur du mur, réserve les cases devant et derrière (aucun meuble) et pose
        // l'objet porte ancré pour que son emprise tournée commence à la case nord-ouest de l'embrasure (face nord ou ouest
        // d'un mur épais), dans le niveau dont la tranche contient la dalle (dalle sur pilotis comprise).
        private ArchOp PlaceDoor(string what, Rect wall, bool alongX, int baseZ, int s0, int w, int h, PlanObject? door, int minX, int minZ)
        {
            int x0 = alongX ? s0 : wall.X0, x1 = alongX ? s0 + w - 1 : wall.X1, y0 = alongX ? wall.Y0 : s0, y1 = alongX ? wall.Y1 : s0 + w - 1;
            var cut = Cut(x0, y0, baseZ + 1, x1, y1, baseZ + h);
            var fronts = DoorFronts.TryGetValue(baseZ, out var set) ? set : DoorFronts[baseZ] = [];
            var cells = DoorCells.TryGetValue(baseZ, out var used) ? used : DoorCells[baseZ] = [];
            var embrasure = Enumerable.Range(y0, y1 - y0 + 1).SelectMany(y => Enumerable.Range(x0, x1 - x0 + 1).Select(x => (x, y))).ToList();
            if (embrasure.Any(cells.Contains) && door is not null) { Notes.Add($"{what} : chevauche une autre porte, porte ignorée"); door = null; }
            cells.UnionWith(embrasure);
            for (var s = s0; s < s0 + w; s++)
            {
                fronts.Add(alongX ? (s, y0 - 1) : (x0 - 1, s));
                fronts.Add(alongX ? (s, y1 + 1) : (x1 + 1, s));
            }
            if (door is null) return cut;
            var k = Doc.LevelIndexAtY(baseZ);
            door.Id = NextDoorId();
            door.X = x0 - minX;
            door.Y = y0 - minZ;
            door.Z += baseZ - Doc.LevelBaseY(k);
            Doc.Levels[k].Objects.Add(door);
            return cut;
        }

        // Mobilier : (x, y) est la case nord-ouest de l'emprise tournée ; l'ancre du jeu s'en déduit (cellules du catalogue
        // tournées par Geometry.Rotate). Posé seulement si toute l'emprise est à l'intérieur d'un volume de ce baseZ (hors
        // anneau de murs), hors cloison et sans chevaucher un autre meuble du niveau ; sinon note et abandon. Z null : posé
        // au sol par ObjectPlacer. Plan existant : ses objets comptent dans les chevauchements, et hors d'un volume connu (pièce
        // dessinée au crayon) il suffit que l'emprise soit libre sur toute la hauteur du meuble, au-dessus d'un bloc.
        public void PlaceFurniture(List<Volume> volumes, List<Partition> partitions)
        {
            var used = new Dictionary<int, HashSet<(int X, int Y)>>();
            ArchLayer? built = null;
            if (Seeded && P.Furniture.Count > 0)
            {
                built = ArchLayer.Evaluate(Doc, PlanValidator.MaxArchitectureHeight);
                var infos = DoorInfos.Concat(Furniture.Values).DistinctBy(i => i.Name).ToDictionary(i => i.Name, StringComparer.Ordinal);
                for (var k = 0; k < Doc.Levels.Count; k++)
                foreach (var o in Doc.Levels[k].Objects)
                {
                    var cells = infos.TryGetValue(o.Type, out var info) && info.Cells.Count > 0 ? info.Cells : [Vec3i.Zero];
                    (used.TryGetValue(k, out var set) ? set : used[k] = []).UnionWith(cells.Select(c => Geometry.Rotate(c, o.Rotation & 3)).Select(c => (o.X + c.X, o.Y + c.Z)));
                }
            }
            foreach (var f in Take(P.Furniture, "mobilier"))
            {
                var what = $"meuble « {f.Type} » en ({f.X},{f.Y})";
                if (!Furniture.TryGetValue(f.Type ?? "", out var info)) { Notes.Add($"{what} : objet inconnu, ignoré"); continue; }
                var baseZ = Math.Clamp(f.BaseZ, 0, MaxBaseZ);
                var k = LevelIndexOf(baseZ);
                if (k < 0) { Notes.Add($"{what} : aucun niveau à z = {baseZ}, ignoré"); continue; }
                var rot = f.Rot & 3;
                var cells = info.Cells.Count > 0 ? info.Cells.Select(c => Geometry.Rotate(c, rot)).ToList() : [Vec3i.Zero];
                var ax = f.X - cells.Min(c => c.X);
                var ay = f.Y - cells.Min(c => c.Z);
                var height = cells.Max(c => c.Y) - cells.Min(c => c.Y) + 1;
                var footprint = cells.Select(c => (X: ax + c.X, Y: ay + c.Z)).Distinct().ToList();
                var room = volumes.FirstOrDefault(v => v.BaseZ == baseZ && footprint.All(c =>
                    c.X >= v.R.X0 + v.Thickness && c.X <= v.R.X1 - v.Thickness && c.Y >= v.R.Y0 + v.Thickness && c.Y <= v.R.Y1 - v.Thickness));
                var free = built is not null && footprint.All(c => built.IsSolid(c.X, c.Y, baseZ) && Enumerable.Range(baseZ + 1, height).All(z => !built.IsSolid(c.X, c.Y, z)));
                if (room is null && !free) { Notes.Add($"{what} : hors d'une pièce de ce niveau, ignoré"); continue; }
                if (room is not null && height > room.Height) { Notes.Add($"{what} : trop haut pour la pièce, ignoré"); continue; }
                if (partitions.Any(p => p.BaseZ == baseZ && footprint.Any(c => c.X >= p.R.X0 && c.X <= p.R.X1 && c.Y >= p.R.Y0 && c.Y <= p.R.Y1))) { Notes.Add($"{what} : sur une cloison, ignoré"); continue; }
                if (DoorFronts.TryGetValue(baseZ, out var fronts) && footprint.Any(fronts.Contains)) { Notes.Add($"{what} : devant une porte, ignoré"); continue; }
                var taken = used.TryGetValue(k, out var set) ? set : used[k] = [];
                if (footprint.Any(taken.Contains)) { Notes.Add($"{what} : chevauche un autre meuble, ignoré"); continue; }
                taken.UnionWith(footprint);
                Doc.Levels[k].Objects.Add(new PlanObject { Id = NextFurnitureId(), Type = info.Name, X = ax, Y = ay, Rotation = rot });
            }
        }
    }
}
