#!/usr/bin/env python3
"""Génère BuildingPlanner/BlockColors.Generated.cs : une couleur par matériau de construction,
extraite de l'icône du jeu (wwwroot/assets/eco-icons/<Nom>Item_FG.png).

    python ecocraft/Scripts/gen-block-colors.py [chemin de l'export v5]

Le jeu n'expose aucune couleur par matériau : sa table officielle (AutoGenBlockColorMap.cs,
la MinimapColor Unity) est par forme et confond ce qu'on cherche justement à séparer (les onze
bois composites y partagent une seule couleur). La couleur dominante de l'icône, elle, distingue
chaque matériau et correspond à ce que le joueur voit en construisant.

Pipeline, en OKLab :
  1. pixels opaques, moins les 25 % les plus sombres (ombres, contours) et le haut du 97e centile
     (spéculaires) ;
  2. L et C médians, teinte = moyenne circulaire pondérée par le chroma (les gris ne tirent pas
     la teinte) ;
  3. L ramené dans [0.42, 0.86] ; le chroma est amplifié (x 1.5, borné) sauf sur les blocs franchement
     neutres — acier, béton, basalte — où la teinte n'est que du bruit d'arrondi : les rehausser leur
     inventerait une couleur, ce qui est précisément le défaut qu'on corrige ;
  4. les matériaux qui se confondent (même teinte, ou tous deux neutres) sont écartés en luminosité
     seulement, dans leur ordre de luminosité réel — teinte et chroma ne sont jamais déformés.
"""
import json, math, os, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ICONS = os.path.join(ROOT, "ecocraft", "wwwroot", "assets", "eco-icons")
OUT = os.path.join(ROOT, "ecocraft", "BuildingPlanner", "BlockColors.Generated.cs")
DEFAULT_EXPORT = os.path.join(os.path.dirname(ROOT), "eco_gnome_data_v5_fix-static.json")

# Doit rester aligné sur Tier0Materials de BuildingPlannerCatalogService : les seuls blocs sans tier
# proposés dans les listes de matériaux.
TIER0 = {"DirtItem", "GardenGravelItem", "StoneRoadItem", "AsphaltConcreteItem"} | {
    "CrushedBasaltItem",
    "CrushedCoalItem",
    "CrushedCopperOreItem",
    "CrushedGneissItem",
    "CrushedGoldOreItem",
    "CrushedGraniteItem",
    "CrushedIronOreItem",
    "CrushedLimestoneItem",
    "CrushedMixedRockItem",
    "CrushedSandstoneItem",
    "CrushedShaleItem",
    "CrushedSlagItem",
    "CrushedSulfurItem",
}

# Quelques blocs en vrac sont illustrés dans une caisse ou sur une palette : c'est le bois de l'emballage qui
# domine l'icône, pas le matériau. Pour ceux-là on part de la couleur minimap du jeu, qui traverse ensuite le
# même pipeline (Eco/Server/Eco.Simulation/WorldLayers/History/AutoGenBlockColorMap.cs).
OVERRIDES = {
    "GardenGravelItem": "#c8c8c8",
    "AsphaltConcreteItem": "#3c3c3c",
}

L_MIN, L_MAX = 0.40, 0.88
L_LO, L_HI = 0.42, 0.86     # plage de normalisation avant écartement
C_MIN, C_MAX = 0.030, 0.145
NEUTRAL_C = 0.020           # en dessous, le bloc est gris : on le laisse gris
SPREAD_STEP = 0.075         # écart de luminosité visé dans une grappe
CLUSTER_H, CLUSTER_C = 18.0, 0.05

M1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929],
               [0.2119034982, 0.6806995451, 0.1073969566],
               [0.0883024619, 0.2817188376, 0.6299787005]])
M2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468],
               [1.9779984951, -2.4285922050, 0.4505937099],
               [0.0259040371, 0.7827717662, -0.8086757660]])
I1, I2 = np.linalg.inv(M1), np.linalg.inv(M2)


def srgb_to_lin(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055) * 255.0


def to_oklab(rgb):
    return np.cbrt(np.maximum(rgb @ M1.T, 0)) @ M2.T


def to_hex(L, C, h):
    hr = math.radians(h)
    lab = np.array([L, C * math.cos(hr), C * math.sin(hr)])
    r, g, b = np.clip(lin_to_srgb(((lab @ I2.T) ** 3) @ I1.T), 0, 255)
    return "#%02x%02x%02x" % (round(r), round(g), round(b))


def extract(path):
    """Couleur dominante d'une icône, en (L, C, teinte) OKLab."""
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64)
    px = a[..., :3][a[..., 3] > 200]
    if len(px) == 0:
        return None
    lab = to_oklab(srgb_to_lin(px))
    L, a_, b_ = lab[:, 0], lab[:, 1], lab[:, 2]
    C, h = np.hypot(a_, b_), np.degrees(np.arctan2(b_, a_)) % 360
    keep = (L >= np.percentile(L, 25)) & (L <= np.percentile(L, 97))
    if keep.sum() < 20:
        keep = np.ones_like(L, bool)
    L, C, h = L[keep], C[keep], h[keep]
    w = C + 1e-4
    hm = math.degrees(math.atan2(np.sum(w * np.sin(np.radians(h))),
                                 np.sum(w * np.cos(np.radians(h))))) % 360
    return float(np.median(L)), float(np.median(C)), hm


def from_hex(value):
    px = np.array([[int(value[i:i + 2], 16) for i in (1, 3, 5)]], dtype=np.float64)
    L, a_, b_ = to_oklab(srgb_to_lin(px))[0]
    return float(L), float(math.hypot(a_, b_)), math.degrees(math.atan2(b_, a_)) % 360


def normalize(L, C, h):
    return (L_LO + (min(max(L, 0.20), 0.95) - 0.20) / 0.75 * (L_HI - L_LO),
            C if C < NEUTRAL_C else min(max(C * 1.5, C_MIN), C_MAX), h)


def confusable(a, b):
    """Deux couleurs que la teinte ne sépare pas : même teinte, ou toutes deux neutres."""
    if a[1] < NEUTRAL_C and b[1] < NEUTRAL_C:
        return True
    dh = abs((a[2] - b[2] + 180) % 360 - 180)
    return dh < CLUSTER_H and abs(a[1] - b[1]) < CLUSTER_C


def spread(cols):
    """Écarte en luminosité les matériaux qu'aucune différence de teinte ne sépare."""
    out, names, seen = dict(cols), list(cols), set()
    for n in names:
        if n in seen:
            continue
        cluster, seen = [n], seen | {n}
        for m in names:
            if m in seen:
                continue
            if confusable(cols[n], cols[m]):
                cluster.append(m)
                seen.add(m)
        if len(cluster) < 2:
            continue
        cluster.sort(key=lambda x: cols[x][0])
        span = min(L_MAX - L_MIN, SPREAD_STEP * (len(cluster) - 1))
        mid = sum(cols[x][0] for x in cluster) / len(cluster)
        start = min(max(mid - span / 2, L_MIN), L_MAX - span)
        for i, x in enumerate(cluster):
            out[x] = (start + span / (len(cluster) - 1) * i, cols[x][1], cols[x][2])
    return out


def main():
    export = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_EXPORT
    if not os.path.exists(export):
        sys.exit("export introuvable : %s" % export)
    data = json.load(open(export, encoding="utf-8-sig"))

    mats = []
    for item in data["Items"]:
        b = item.get("BuildingBlock")
        if not b or not b.get("IsWall") or b.get("IgnoreRooms"):
            continue
        if b["Tier"] >= 1 or item["Name"] in TIER0:
            mats.append((item["Name"], b["Tier"], item["LocalizedName"]["en-US"]))

    raw, skipped = {}, []
    for name, _, _ in mats:
        if name in OVERRIDES:
            raw[name] = normalize(*from_hex(OVERRIDES[name]))
            continue
        path = os.path.join(ICONS, name + "_FG.png")
        got = extract(path) if os.path.exists(path) else None
        if got is None:
            skipped.append(name)
        else:
            raw[name] = normalize(*got)

    final = spread(raw)
    lines = []
    for name, tier, label in sorted(mats, key=lambda m: (m[1], m[2])):
        if name in final:
            lines.append('        { "%s", "%s" },%s// T%d %s'
                         % (name, to_hex(*final[name]), " " * max(1, 40 - len(name)), tier, label))

    with open(OUT, "w", encoding="utf-8-sig", newline="\r\n") as f:
        f.write("// <auto-generated />\n")
        f.write("// Régénérer : python ecocraft/Scripts/gen-block-colors.py [export v5]\n")
        f.write("// Couleur dominante de l'icône du bloc, écartée en luminosité entre matériaux confondables.\n")
        f.write("\nusing System.Collections.Frozen;\n\nnamespace ecocraft.BuildingPlanner;\n\n")
        f.write("public static partial class BlockColors\n{\n")
        f.write("    static readonly FrozenDictionary<string, string> Generated = new Dictionary<string, string>(StringComparer.Ordinal)\n    {\n")
        f.write("\n".join(lines))
        f.write("\n    }.ToFrozenDictionary(StringComparer.Ordinal);\n}\n")

    ls = [v[0] for v in final.values()]
    print("%d matériaux écrits dans %s" % (len(lines), os.path.relpath(OUT, ROOT)))
    print("luminosité [%.3f, %.3f]" % (min(ls), max(ls)))
    if skipped:
        print("sans icône, ignorés : %s" % ", ".join(skipped))


if __name__ == "__main__":
    main()
