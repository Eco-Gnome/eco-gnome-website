using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Formes de mur. « Wall » (tous les sets sauf FarEast et Adobe) se raccorde seul : coins, T et bouts viennent des voisins.
// Les murs FarEast Wall_01..19 et Adobe WallMid sont posés tournés par le joueur (meshes mesurés, mêmes conventions dans les
// deux sets : rot 0 le long de x, rot 1 le long de y) : le générateur pose chaque côté avec sa rotation, un WallCorner à
// chaque angle et un WallT où une cloison rejoint un mur.
public static partial class BuildingGenerator
{
    public static readonly string[] WallForms = ["Wall", "WallMid", .. Enumerable.Range(1, 19).Select(i => $"Wall_{i:00}")];

    // Sets dont les murs, l'escalier et la fenêtre ont leurs propres formes (pas de Wall ni de Stairs).
    private static bool IsFarEast(string material) => material == "FarEastLumberItem";
    private static bool IsAdobe(string material) => material == "AdobeItem";

    // Mur par défaut du matériau.
    private static string DefaultWallForm(string material) => IsFarEast(material) ? "Wall_01" : IsAdobe(material) ? "WallMid" : "Wall";

    // Fenêtre au marteau Window (pas de WindowGrilles) : FarEast (posée tournée), acier ondulé, béton, pierres de taille (Ashlar)
    // et acier plat (raccordée à ses voisins).
    private static bool UsesWindowForm(string material)
        => IsFarEast(material) || IsAdobe(material) || material is "CorrugatedSteelItem" or "ReinforcedConcreteItem" or "FlatSteelItem" || material.StartsWith("Ashlar", StringComparison.Ordinal);

    // Style des coins, T et X assorti au mur : 01 uni, 02 cadre haut avec panneau, 03 cadre haut et bas, 04 uni avec panneau.
    private static string JunctionStyle(string wallForm) => wallForm switch
    {
        "Wall_01" => "01",
        "Wall_02" or "Wall_17" => "02",
        "Wall_04" => "04",
        _ => "03",
    };

    // Angle, T ou X assorti au mur tourné : WallCorner, WallT, WallX en Adobe ; suffixe de style en FarEast.
    private static string Junction(string kind, string wallForm) => wallForm == "WallMid" ? kind : $"{kind}_{JunctionStyle(wallForm)}";

    // Angle d'une boîte : bras du WallCorner vers l'intérieur des deux côtés (r0 bras ouest + nord … r3 est + nord).
    private static int CornerWallRot(bool west, bool north) => north ? (west ? 2 : 1) : (west ? 3 : 0);

    // WallT dont la branche regarde dir(d) (est, sud, ouest, nord) : r3, r2, r1, r0.
    private static int TeeWallRot(int d) => (3 - d + 4) % 4;

    private sealed partial class Build
    {
        // Forme des murs d'un volume ou d'une cloison : celle demandée si le matériau l'a, sinon celle du matériau.
        public string WallFormFor(string? requested, string material, string what)
        {
            var fallback = DefaultWallForm(material);
            if (string.IsNullOrEmpty(requested)) return fallback;
            if (!WallForms.Contains(requested, StringComparer.Ordinal)) { Notes.Add($"{what} : forme de mur « {requested} » inconnue, remplacée par {fallback}"); return fallback; }
            if (IsFarEast(material) ? !requested.StartsWith("Wall_", StringComparison.Ordinal) : requested != fallback) { Notes.Add($"{what} : forme de mur « {requested} » absente de {material}, remplacée par {fallback}"); return fallback; }
            return requested;
        }

        // Anneau de murs posés tournés (épaisseur 1) : quatre côtés sans les angles, puis un WallCorner par angle.
        public List<ArchOp> FramedRing(Volume v) => RotatedRing(v.R, v.BaseZ + 1, v.BaseZ + v.Height, v.Wall, v.WallForm, Junction("WallCorner", v.WallForm));

        // Anneau de pièces posées tournées (murs, parapets Adobe) : form le long des côtés, corner aux angles.
        private static List<ArchOp> RotatedRing(Rect r, int z0, int z1, string material, string form, string corner) =>
        [
            Box(r.X0 + 1, r.Y0, z0, r.X1 - 1, r.Y0, z1, material, form, 0),
            Box(r.X0 + 1, r.Y1, z0, r.X1 - 1, r.Y1, z1, material, form, 0),
            Box(r.X0, r.Y0 + 1, z0, r.X0, r.Y1 - 1, z1, material, form, 1),
            Box(r.X1, r.Y0 + 1, z0, r.X1, r.Y1 - 1, z1, material, form, 1),
            Box(r.X0, r.Y0, z0, r.X0, r.Y0, z1, material, corner, CornerWallRot(true, true)),
            Box(r.X1, r.Y0, z0, r.X1, r.Y0, z1, material, corner, CornerWallRot(false, true)),
            Box(r.X1, r.Y1, z0, r.X1, r.Y1, z1, material, corner, CornerWallRot(false, false)),
            Box(r.X0, r.Y1, z0, r.X0, r.Y1, z1, material, corner, CornerWallRot(true, false)),
        ];

        // Jonctions des cloisons tournées : chaque bout touche un mur tourné (côté de volume ou autre cloison, même niveau)
        // sur sa dernière case si elle empiète sur le mur, sinon sur la case suivante. Une branche → WallT tourné vers la
        // cloison, deux branches opposées → WallX. Émis après toutes les cloisons (elles ne les écrasent pas) ; angles laissés.
        public List<List<ArchOp>> Junctions(List<Partition> partitions, List<Volume> volumes)
        {
            var hits = new List<(int X, int Y, int Stem, int Z0, int Z1, string Material, string Form)>();
            foreach (var p in partitions.Where(p => p.WallForm != "Wall"))
            foreach (var (x, y, d) in p.AlongX
                ? new[] { (p.R.X0, p.R.Y0, 2), (p.R.X1, p.R.Y0, 0) }
                : new[] { (p.R.X0, p.R.Y0, 3), (p.R.X0, p.R.Y1, 1) })
            {
                var (dx, dy) = Dir[d];
                foreach (var (cx, cy) in new[] { (x, y), (x + dx, y + dy) })
                {
                    var v = volumes.FirstOrDefault(v => v.BaseZ == p.BaseZ && v.WallForm != "Wall" && OnRingSide(v.R, cx, cy));
                    var q = partitions.FirstOrDefault(q => q != p && q.BaseZ == p.BaseZ && q.WallForm != "Wall" && q.AlongX != p.AlongX && Inside(q.R, cx, cy));
                    var (material, form, top) = v is not null ? (v.Wall, v.WallForm, v.BaseZ + v.Height) : q is not null ? (q.Material, q.WallForm, q.BaseZ + q.Height) : ("", "", 0);
                    if (form.Length == 0) continue;
                    hits.Add((cx, cy, (d + 2) % 4, p.BaseZ + 1, Math.Min(p.BaseZ + p.Height, top), material, form));
                    break;
                }
            }
            return hits.GroupBy(h => (h.X, h.Y)).Select(g =>
            {
                var h = g.First();
                var cross = g.Select(k => k.Stem).Distinct().Count() > 1;
                var form = Junction(cross ? "WallX" : "WallT", h.Form);
                return new List<ArchOp> { Box(h.X, h.Y, g.Min(k => k.Z0), h.X, h.Y, g.Max(k => k.Z1), h.Material, form, cross ? 0 : TeeWallRot(h.Stem)) };
            }).ToList();
        }

        private static bool Inside(Rect r, int x, int y) => x >= r.X0 && x <= r.X1 && y >= r.Y0 && y <= r.Y1;

        // Case d'un côté de l'anneau, angles exclus.
        private static bool OnRingSide(Rect r, int x, int y)
        {
            var onX = (y == r.Y0 || y == r.Y1) && x > r.X0 && x < r.X1;
            var onY = (x == r.X0 || x == r.X1) && y > r.Y0 && y < r.Y1;
            return onX || onY;
        }
    }
}
