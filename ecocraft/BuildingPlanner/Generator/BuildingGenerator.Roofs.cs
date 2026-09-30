using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Toits, en coordonnées (u, v) : u le long du faîte, v en travers (l'axe de la pente). Une course = une couche z et une
// rangée : les blocs de toit du jeu ont une pente fixe de 45°, deux rangées à la même hauteur feraient des marches. Le
// pan vers +v porte RoofSide orienté vers +v (la pente descend vers le bord bas), le faîte RoofPeak le long de u, les
// angles de croupe RoofCorner ; en FarEast la première course est en rives relevées (RoofEdgeSide, RoofEdgeCorner).
// courses > 0 arrête le toit après ce nombre de courses, sans faîte ni pignons : un auvent autour de l'étage du dessus.
public static partial class BuildingGenerator
{
    private sealed record Roof(Rect Outer, Rect Inner, int BaseZ, string Material, bool RidgeAlongY, int Courses, string Style, bool Parapet)
    {
        public bool Capped { get; set; }
        public int Limit => Courses > 0 ? Courses : MaxCourses;
        public int U0 => RidgeAlongY ? Outer.Y0 : Outer.X0;
        public int U1 => RidgeAlongY ? Outer.Y1 : Outer.X1;
        public int V0 => RidgeAlongY ? Outer.X0 : Outer.Y0;
        public int V1 => RidgeAlongY ? Outer.X1 : Outer.Y1;
        public int InnerU0 => RidgeAlongY ? Inner.Y0 : Inner.X0;
        public int InnerU1 => RidgeAlongY ? Inner.Y1 : Inner.X1;
        public int InnerV0 => RidgeAlongY ? Inner.X0 : Inner.Y0;
        public int InnerV1 => RidgeAlongY ? Inner.X1 : Inner.Y1;

        // Boîte u0..u1 × va..vb sur la couche z.
        public ArchOp Strip(int u0, int u1, int va, int vb, int z, string form, int rot)
            => RidgeAlongY ? Box(va, u0, z, vb, u1, z, Material, form, rot) : Box(u0, va, z, u1, vb, z, Material, form, rot);

        public (int X, int Y) ToXY(int u, int v) => RidgeAlongY ? (v, u) : (u, v);

        // Rotation (jeu) de la pente qui descend vers +v / −v / +u / −u.
        public int TowardV(bool positive) => LowToward(RidgeAlongY ? (positive ? 0 : 2) : (positive ? 1 : 3));
        public int TowardU(bool positive) => LowToward(RidgeAlongY ? (positive ? 1 : 3) : (positive ? 0 : 2));
        public int PeakRot => RidgeAlongY ? 1 : 0;

        // Pan et angle de la course k : rives relevées FarEast sur la première.
        public string Side(int k) => k == 0 && IsFarEast(Material) ? "RoofEdgeSide" : "RoofSide";
        public string Corner(int k) => k == 0 && IsFarEast(Material) ? "RoofEdgeCorner" : "RoofCorner";

        // Fin d'un toit qui n'a pas atteint son faîte : rien pour un auvent, un plateau au-delà de MaxCourses.
        public void Top(List<ArchOp> ops, int u0, int u1, int va, int vb, int z)
        {
            if (Courses > 0) return;
            Capped = true;
            ops.Add(Strip(u0, u1, va, vb, z, "Cube", 0));
        }
    }

    private sealed partial class Build
    {
        public List<Roof> NormalizeRoofs() => Take(P.Roofs, "toits").Select((r, i) =>
        {
            var what = $"toit n° {i + 1}";
            var inner = ClampRect(r.X, r.Y, r.Width, r.Depth, 1, what);
            var overhang = Math.Clamp(r.Overhang, 0, 2);
            var outer = new Rect(Math.Max(0, inner.X0 - overhang), Math.Max(0, inner.Y0 - overhang), Math.Min(W - 1, inner.X1 + overhang), Math.Min(D - 1, inner.Y1 + overhang));
            var style = r.Style is "flat" or "shed" or "hip" ? r.Style : "gable";
            return new Roof(outer, inner, Math.Clamp(r.BaseZ, 0, MaxBaseZ), Material(r.Material, what), r.RidgeAxis == "y", Math.Clamp(r.Courses, 0, MaxCourses), style, r.Parapet);
        }).ToList();

        public void EmitRoofs(List<Roof> roofs, List<Volume> volumes, List<Partition> partitions)
        {
            EmitCategory("toits", roofs.Select((roof, i) =>
            {
                var ops = roof.Style switch
                {
                    "flat" => Flat(roof, roof.Parapet),
                    "shed" => Shed(roof),
                    "hip" => Hip(roof),
                    _ => Gable(roof),
                };
                if (roof.Capped) Notes.Add($"toit n° {i + 1} : plus de {MaxCourses} courses, le haut est un toit plat");
                ops.AddRange(Fillers(roof, ops, volumes.Where(v => OpenTo(v, roof, volumes)).ToList(), partitions));
                return ops;
            }));
        }

        // Pièce ouverte jusqu'au toit, sans plafond : toit à deux ou quatre pans jusqu'au faîte (courses 0) dont l'emprise couvre
        // le volume, posé sur ses murs (baseZ = plafond) ou une couche plus haut, sans volume posé dessus ni flatCeiling.
        public static bool OpenTo(Volume v, Roof r, List<Volume> volumes)
        {
            var ceiling = v.BaseZ + v.Height + 1;
            return !v.FlatCeiling && r.Style is "gable" or "hip" && r.Courses == 0 && r.BaseZ - ceiling is 0 or 1
                && r.Inner.X0 <= v.R.X0 && r.Inner.Y0 <= v.R.Y0 && r.Inner.X1 >= v.R.X1 && r.Inner.Y1 >= v.R.Y1
                && !volumes.Any(u => u.BaseZ == ceiling && u.R.X0 <= v.R.X1 && u.R.X1 >= v.R.X0 && u.R.Y0 <= v.R.Y1 && u.R.Y1 >= v.R.Y0);
        }

        // Remplissage sous le toit d'une pièce ouverte (Cube, matériau du toit), comme en jeu : une pente à 45° n'a qu'un bloc par
        // colonne, l'air sous chaque marche voit en diagonale l'air du dehors au-dessus de la marche d'en dessous (arête vide,
        // tier 0). Un bloc sous la case la plus basse du toit de chaque colonne de l'emprise les bouche tous (pans, croupes,
        // faîte, pignons). L'anneau des murs et les cloisons pleine hauteur montent jusqu'au toit, depuis l'ancien plafond : la
        // pièce reste fermée sous le débord et les pièces restent séparées.
        private List<ArchOp> Fillers(Roof r, List<ArchOp> roofOps, List<Volume> open, List<Partition> partitions)
        {
            var low = new Dictionary<(int X, int Y), int>();
            foreach (var op in roofOps) ArchShapes.Paint(op, W, D, PlanValidator.MaxArchitectureHeight, (x, y, z) => low[(x, y)] = Math.Min(z, low.GetValueOrDefault((x, y), int.MaxValue)));
            var cells = new List<int>();
            foreach (var v in open)
            {
                var ceiling = v.BaseZ + v.Height + 1;
                var walls = partitions.Where(p => p.BaseZ == v.BaseZ && p.Height >= v.Height).ToList();
                for (var y = v.R.Y0; y <= v.R.Y1; y++)
                for (var x = v.R.X0; x <= v.R.X1; x++)
                {
                    if (!low.TryGetValue((x, y), out var top)) continue;
                    var full = x == v.R.X0 || x == v.R.X1 || y == v.R.Y0 || y == v.R.Y1 || walls.Any(p => Inside(p.R, x, y));
                    for (var z = Math.Max(ceiling, full ? 0 : top - 1); z < top; z++) cells.AddRange([x, y, z]);
                }
            }
            return cells.Chunk(3 * PlanValidator.MaxCellsPerOp).Select(c => Cells([.. c], r.Material, "Cube", 0)).ToList();
        }

        private static List<ArchOp> Flat(Roof r, bool parapet)
        {
            if (IsAdobe(r.Material)) return AdobeFlat(r, parapet);
            var ops = new List<ArchOp> { Box(r.Outer, r.BaseZ, r.BaseZ, r.Material, "Cube") };
            if (parapet && r.Outer.X1 - r.Outer.X0 >= 2 && r.Outer.Y1 - r.Outer.Y0 >= 2) ops.Add(Box(r.Outer, r.BaseZ + 1, r.BaseZ + 1, r.Material, "Cube", hollow: 1));
            return ops;
        }

        // Toit plat en adobe, comme en jeu : parapets RoofMid / RoofCorner sur les murs (sans débord) et dalle RoofFill au même
        // niveau ; le décor des parapets fait sortir les poutres côté vide (dehors). Parapet : muret WallMid au-dessus.
        private static List<ArchOp> AdobeFlat(Roof r, bool parapet)
        {
            var (i, z) = (r.Inner, r.BaseZ);
            if (i.X1 - i.X0 < 2 || i.Y1 - i.Y0 < 2) return [Box(i, z, z, r.Material, "RoofFill")];
            var ops = new List<ArchOp> { Box(i.X0 + 1, i.Y0 + 1, z, i.X1 - 1, i.Y1 - 1, z, r.Material, "RoofFill") };
            ops.AddRange(RotatedRing(i, z, z, r.Material, "RoofMid", "RoofCorner"));
            if (parapet) ops.AddRange(RotatedRing(i, z + 1, z + 1, r.Material, "WallMid", "WallCorner"));
            return ops;
        }

        // Un pan : la course k est la rangée k depuis le bord bas (+v), en remontant vers −v.
        private static List<ArchOp> Shed(Roof r)
        {
            var ops = new List<ArchOp>();
            for (int hi = r.V1, k = 0; hi >= r.V0; k++, hi--)
            {
                if (k == r.Limit) { r.Top(ops, r.U0, r.U1, r.V0, hi, r.BaseZ + k); break; }
                ops.Add(r.Strip(r.U0, r.U1, hi, hi, r.BaseZ + k, r.Side(k), r.TowardV(true)));
            }
            return ops;
        }

        // Deux pans symétriques qui se rejoignent sur un faîte de 1 ou 2 rangées ; pignons pleins (Cube) aux deux bouts
        // de l'emprise intérieure, entre les pans (pas sur un auvent : l'étage du dessus est là).
        private static List<ArchOp> Gable(Roof r)
        {
            var ops = new List<ArchOp>();
            var gableA = new List<int>();
            var gableB = new List<int>();
            int lo = r.V0, hi = r.V1, k = 0;
            for (; hi - lo + 1 > 2 && k < r.Limit; k++)
            {
                var z = r.BaseZ + k;
                ops.Add(r.Strip(r.U0, r.U1, lo, lo, z, r.Side(k), r.TowardV(false)));
                ops.Add(r.Strip(r.U0, r.U1, hi, hi, z, r.Side(k), r.TowardV(true)));
                for (var v = Math.Max(lo + 1, r.InnerV0); r.Courses == 0 && v <= Math.Min(hi - 1, r.InnerV1); v++)
                {
                    var (xa, ya) = r.ToXY(r.InnerU0, v);
                    var (xb, yb) = r.ToXY(r.InnerU1, v);
                    gableA.AddRange([xa, ya, z]);
                    if (r.InnerU1 != r.InnerU0) gableB.AddRange([xb, yb, z]);
                }
                lo++; hi--;
            }
            if (hi - lo + 1 <= 2) ops.Add(r.Strip(r.U0, r.U1, lo, hi, r.BaseZ + k, "RoofPeak", r.PeakRot));
            else r.Top(ops, r.U0, r.U1, lo, hi, r.BaseZ + k);
            if (gableA.Count > 0) ops.Add(Cells(gableA, r.Material, "Cube", 0));
            if (gableB.Count > 0) ops.Add(Cells(gableB, r.Material, "Cube", 0));
            return ops;
        }

        // Quatre pans : anneau par course (deux bandes en travers pleine longueur, deux bandes le long entre elles),
        // angle extérieur de chaque coin en RoofCorner (une op cells par forme et orientation), faîte sur ce qui reste.
        private static List<ArchOp> Hip(Roof r)
        {
            var ops = new List<ArchOp>();
            var corners = new Dictionary<(string Form, int Rot), List<int>>();
            int lu = r.U0, hu = r.U1, lv = r.V0, hv = r.V1, k = 0;
            for (; Math.Min(hu - lu, hv - lv) + 1 > 2 && k < r.Limit; k++)
            {
                var z = r.BaseZ + k;
                ops.Add(r.Strip(lu, hu, lv, lv, z, r.Side(k), r.TowardV(false)));
                ops.Add(r.Strip(lu, hu, hv, hv, z, r.Side(k), r.TowardV(true)));
                ops.Add(r.Strip(lu, lu, lv + 1, hv - 1, z, r.Side(k), r.TowardU(false)));
                ops.Add(r.Strip(hu, hu, lv + 1, hv - 1, z, r.Side(k), r.TowardU(true)));
                foreach (var (u, v, uNeg, vNeg) in new[] { (lu, lv, true, true), (hu, lv, false, true), (hu, hv, false, false), (lu, hv, true, false) })
                {
                    var (x, y) = r.ToXY(u, v);
                    var west = r.RidgeAlongY ? vNeg : uNeg;
                    var north = r.RidgeAlongY ? uNeg : vNeg;
                    var key = (r.Corner(k), CornerRot(west, north, r.Material));
                    if (!corners.TryGetValue(key, out var cells)) corners[key] = cells = [];
                    cells.AddRange([x, y, z]);
                }
                lu++; hu--; lv++; hv--;
            }
            var alongU = hv - lv <= hu - lu;
            if (Math.Min(hu - lu, hv - lv) + 1 <= 2) ops.Add(r.Strip(lu, hu, lv, hv, r.BaseZ + k, "RoofPeak", alongU ? r.PeakRot : 1 - r.PeakRot));
            else r.Top(ops, lu, hu, lv, hv, r.BaseZ + k);
            foreach (var ((form, rot), cells) in corners.OrderBy(c => c.Key.Form, StringComparer.Ordinal).ThenBy(c => c.Key.Rot))
                ops.Add(Cells(cells, r.Material, form, rot));
            return ops;
        }

        // Angle bas entre dir(d) et dir(d+1) : sud-est d = 0, sud-ouest 1, nord-ouest 2, nord-est 3 → rotation du jeu. Angle bas
        // du coin pour rot 0..3 (meshes extraits) : S-E, N-E, N-O, S-O en FarEast et pour le coin CL (béton, T5), comme RoofSide ;
        // un quart de tour plus loin (S-O, S-E, N-E, N-O) pour le coin MS_RoofCorner de Hewn Logs, Mortared Stone, Brick, Lumber
        // et de l'acier ondulé.
        private static int CornerRot(bool west, bool north, string material)
            => (LowToward(north ? (west ? 2 : 3) : (west ? 1 : 0)) + (IsFarEast(material) || HasClRoofCorner(material) ? 0 : 1)) % 4;

        // Coin de toit dessiné comme celui du Composite Lumber : béton, T5 (pierres de taille, Composite Lumber, acier plat, verre encadré).
        private static bool HasClRoofCorner(string material)
            => material is "ReinforcedConcreteItem" or "FlatSteelItem" or "FramedGlassItem"
               || material.StartsWith("Ashlar", StringComparison.Ordinal) || material.StartsWith("Composite", StringComparison.Ordinal);
    }
}
