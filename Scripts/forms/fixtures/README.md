# Fixture de calibration des formes

`calibration.json` est un `PlanDocument` v3 (mode maison, grille 24×16, hauteur d'architecture 8) à charger par
l'import JSON du building planner, puis à comparer avec la même disposition construite en jeu en Mortared Sandstone.
Il fixe la matrice Unity → planner (`--axis`) et les décalages de rotation par forme (`--base-offset`) de
`extract_forms.py`. Chaque op porte un `name` (ignoré par le schéma, documentaire).

Les cellules sont `box` d'une seule cellule à z = 1 (première couche d'air du rez), sauf mention. `rot` est la
rotation du bloc en jeu (`Stairs`, `Stairs90`, `Stairs180`, `Stairs270` → 0..3). Le jeu tourne en sens inverse du
planner : d'après les meshes extraits, le côté bas regarde `[est, nord, ouest, sud][r]` (x croît vers l'est, y vers
le sud). C'est ce que le générateur suppose (`BuildingGenerator.LowToward`) ; la comparaison en jeu le confirme ou
l'infirme.

| Rangée (y) | Forme | Cellules (x) | Attendu en jeu |
|---|---|---|---|
| 1 | `Stairs` rot 0..3 | 1, 4, 7, 10 | première marche (côté bas) à l'est, au nord, à l'ouest, au sud |
| 4 | `RoofSide` rot 0..3 | 1, 4, 7, 10 | la pente descend vers l'est, le nord, l'ouest, le sud |
| 7 | `RoofCorner` rot 0..3 | 1, 4, 7, 10 | angle bas au sud-ouest, sud-est, nord-est, nord-ouest (un quart de tour après `RoofSide` : meshes `MS_` en miroir X ; FarEast : sud-est, nord-est, nord-ouest, sud-ouest) |
| 10 | `RoofPeak` rot 0..3 | 1, 4, 7, 10 | faîte le long de x pour r ∈ {0, 2}, de y pour r ∈ {1, 3} |
| 13 | `RoofTurn` rot 0..3 | 1, 4, 7, 10 | angle rentrant, même ordre que `RoofCorner` (non émis par le générateur) |
| 1..4 | `Wall` anneau creux 6×4, hauteur 2 (z = 1..2) | x ∈ [14, 19] | coins `WallCorner`, T `T_Wall` en (16,1) et (16,4), droits `Wall` ; la rangée z = 2 vérifie qu'un mur posé sur un mur reste mince et bien orienté (cas « voisin du dessous » du builder) |
| 2..3 | `Wall` cloison, hauteur 2 | x = 16 | murs droits reliés à l'anneau |
| 2 | `Column` pile de 3 (z = 1..3) | x = 22 | bas / milieu / haut |
| 5 | `Cube` | x = 22 | cube plein texturé |

Si une forme est tournée d'un quart de tour constant, corriger `--base-offset Forme=deg` (degrés Unity). Si l'axe
y est inversé (sud/nord), corriger `--axis` (ex. `x,-z,y`) : c'est la seule constante commune à toutes les formes.

# Ranch Hewn Log

`ranch-hewn-log.json` montre toutes les formes Hewn Logs rendues en 3D : maison de trois pièces sur pilotis de 1
(`Column`), terrasse (`Floor`) bordée de barrières à corde (`DocksFenceMid`, `DocksFenceCorner`, `DocksFenceEndCap`),
escalier, chemin en planches (`DocksPlatformFill`), rampe douce (`DocksRampA`→`D`), ponton (`DocksPlatform` sur
`DocksColumn`, `DocksBarrelPlatform` au bout, portique relevé en jeu : deux colonnes voisines de `DocksPillar`, `DocksPillarBeamJunction` (haut de poteau + poutre
+ jambes de force) aux croisements, `DocksPillarBeam` et bouts `DocksPillarBeamEnd`/`EndAlt` aux extrémités), échelle, logs
empilés, poteau seul. La maison vient du générateur (`Scripts/gen-bench/fixtures/hewn-log-ranch.json`), le reste est
ajouté à la main. Sa dalle est sur la couche 1 : le planner n'y crée pas de niveau (il en faut au moins deux couches
d'écart), les portes restent donc des ouvertures sans objet porte. Rotations retenues d'après les meshes (à confirmer
en jeu, meshes en miroir X comme dans le jeu) : barrière `Mid` r0 le long de y, r1 le long de x ; `Corner` r0 = angle
nord-ouest, r1 sud-ouest, r2 sud-est, r3 nord-est ; `EndCap` r0 capuchon au nord d'une barrière le long de y, r2 au sud,
r1 à l'ouest / r3 à l'est d'une barrière le long de x ; rampes A→D r0 montent vers l'ouest, r1 vers le sud, r2 vers
l'est ; `Ladder` r0 adossée à la face sud ; `DocksPillar` r0 poteau au bord ouest de sa cellule, r2 à l'est ;
`DocksPillarBeamJunction` r0 poteau à l'ouest, r2 à l'est ; `DocksPillarBeamEnd` r0 = bout ouest d'une poutre le long de x,
`DocksPillarBeamEndAlt` r0 = bout est. Une poutre posée au-dessus d'un poteau flotte, en jeu aussi.

# Vitrine FarEast

`fareast-showcase.json` (généré par `make_fareast_showcase.py` depuis le bundle) pose les 70 formes FarEast : les 19
murs en rotations 0 et 1, coins / T / X, colonnes, fenêtres, cube, empilés, échelle adossée à un mur ; une boucle de
barrières sur 2 hauteurs (coins, T, croix, bouts) ; chaque escalier en 4 rotations et une volée de 3 × 3 ; dalle et
plafond 5 × 4 ; chaque forme de toit en 4 rotations ; deux maisons 7 × 5 à toit à 4 pans (rives `RoofEdge*` sur la
seconde). Orientation des coins et des barrières choisie d'après la bbox des meshes, à confirmer en jeu.

# Vitrines des sets

`make_set_showcase.py <bundle> <sortie>` pose toutes les formes d'un set (sauf FarEast, voir plus haut) : formes orientées
en 4 rotations (première skin), puis les motifs des formes que le set possède (murs en L et en T, fenêtres dans un mur,
dalle, piles, toit à quatre pans aux rotations du générateur, toit intelligent `Roof` en pyramide sur une couronne de
`RoofCube`, faîtes `RoofPeakSet` en croix et en L, quais avec le portique relevé en jeu, tuyaux, gravier, verre), enfin une
rangée de toutes les formes par autre skin. Vitrines produites : `hewn-showcase.json`, `mortared-stone-showcase.json`,
`brick-showcase.json`, `lumber-showcase.json`, `garden-gravel-showcase.json`, `pipes-showcase.json`, `glass-showcase.json`
(vitres `Window` en croix, T et L, mur de cubes de verre 5 × 3, vitres horizontales), `corrugated-steel-showcase.json` et
`reinforced-concrete-showcase.json` (T4 : `DoubleWindow` dans un mur, `FlatRoof` 5 × 4, clôture en L et en T, barrière
de route en croix, en L et seule, faîtes `PeakSet` / `UnderPeakSet` du béton), `ashlar-showcase.json`,
`composite-lumber-showcase.json` (bois de chêne), `flat-steel-showcase.json` et `framed-glass-showcase.json` (T5 : murs
`FullWall`, `WallTrim`, `CladWall`, `WindowWall` et garde-corps `SideFence` en L, poteaux `WindowCorners` de l'acier plat
entre des vitres en L et en T, toit intelligent de l'acier plat et du verre encadré), `adobe-showcase.json` (anneau de
murs tournés sur 3 couches, ouvert d'une case, angles, T, mur libre fini par un `WallEnd`, toit plat du jeu : parapet autour d'une dalle `RoofFill`, poutres dehors),
`asphalt-road-showcase.json` et `stone-road-showcase.json` (chaussée 10 × 5, lignes peintes de l'asphalte, rampes),
`carpet-showcase.json` (trois tissus), `terrain-showcase.json` (un bloc 2 × 2 × 2 par matériau).
Les tuyaux ont en plus une chaîne de machines (prises = cellules Solid du catalogue) : pompe mécanique → cuve en bois → machine
à vapeur, conduite qui traverse un mur, cheminée qui traverse une dalle, poêle dont la cheminée traverse un mur. Le bloc
traversé reste dessiné autour du tuyau (`vox.under`), comme en jeu.

# Schéma

Les vitrines produites par les scripts sont en schéma 4 : tout le bâti est dans les ops, le planner n'ajoute ni sol ni
plafond. Le comble du toit à quatre pans des vitrines de sets est une poche d'air fermée : le planner y montre une pièce,
sans poser de bloc. `calibration.json` et `ranch-hewn-log.json` restent en v3 : l'import les migre (aucune pièce
déclarée, donc rien à matérialiser).
