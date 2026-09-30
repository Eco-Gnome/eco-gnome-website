# Formes et textures des blocs d'Eco dans le building planner

Chaîne complète : les assets du jeu (checkout SVN `Eco/Content/Art`, jamais commité) → `extract_forms.py` →
bundles `ecocraft/wwwroot/assets/forms/<Set>.json` + `<Set>.webp` (gitignorés, livrés dans le volume Docker
`app-assets`) → `building-planner.js` (section « Formes de blocs ») qui dessine chaque cellule à forme avec le vrai
mesh et les vraies textures. Les objets (portes, mobilier) suivent le même principe avec `extract_objects.py`.

## Commandes

```
# un set = un dossier de Eco/Content/Art/Blocks/Player Built Blocks/Full Building Types/
python Scripts/forms/extract_forms.py --content "<checkout>/Eco/Content/Art" --set "Mortared Stone" --out ecocraft/wwwroot/assets/forms
# le verre est rangé à part (Player Built Blocks/Glass) : même commande, --set Glass
# Composite Lumber : un bundle par bois (atlas limité à 64 textures), --skin = préfixe des blocs, 11 fois
python Scripts/forms/extract_forms.py --content "<checkout>/Eco/Content/Art" --set "Composite Lumber" --skin CompositeOakLumber --out ecocraft/wwwroot/assets/forms
python Scripts/forms/check_bundle.py ecocraft/wwwroot/assets/forms/MortaredStone.json      # tables Wall/Column, bbox, RESULT OK
# tuyaux cuivre, fer, acier (blockset et builders à part) → Pipes.json ; check_bundle vérifie les 64 combinaisons de voisins
python Scripts/forms/extract_pipes.py --content "<checkout>/Eco/Content/Art" --out ecocraft/wwwroot/assets/forms
# routes (--set "Asphalt Road", "Stone Road"), tapis (--set "Fabric Blocks"), Adobe (--set "Adobe Brick") : extract_forms.py
# terre et roches concassées (textures seules, cube généré par le JS) → Terrain.json
python Scripts/forms/extract_terrain.py --content "<checkout>/Eco/Content/Art" --out ecocraft/wwwroot/assets/forms
cd Scripts/forms/tests && python make_stub.py && node test_shapes.js                         # 286 tests des fonctions JS pures
cd Scripts/gen-bench && dotnet run                                                          # générateur : fixtures → out/*.json importables
# livraison locale (le volume garde les bundles entre deux recreate)
MSYS_NO_PATHCONV=1 docker cp ecocraft/wwwroot/assets/forms eco-gnome-website-app-1:/app/wwwroot/assets/
```

Sets extraits à ce jour : Mortared Stone (4 skins), Hewn Logs (3, complet : les 40 formes au marteau dont 26 de quai,
le toit intelligent `Roof` + `RoofPeakSet` + `RoofCube`, plus les logs empilés), Brick (complet, voir plus bas), Lumber (3, complet :
les 16 formes au marteau de `Lumber.cs` ; barrière `Fence` contextuelle, matériau `… Building Fence.mat` par skin, cas du jeu
repris tels quels, y compris ceux qui regardent les voisins en diagonale dessous), FarEast (complet :
70 formes, voir plus bas), Garden Gravel, Glass (complet, voir plus bas), Corrugated Steel et Reinforced Concrete (T4, complets,
voir plus bas), Ashlar Stone, Composite Lumber, Flat Steel et Framed Glass (T5, complets, voir plus bas), Adobe (complet), routes
en asphalte et en pierre, tapis (3 tissus), terrain (terre + roches concassées, textures seules), tuyaux (3 métaux). Tous
les matériaux de la palette du planner ont leur bundle. Le toit intelligent est aussi pris dans Mortared Stone, Brick et Lumber. Les empilés (Stacked1-3) sont pris dans chaque set qui
les a (pas le gravier : builders absents du checkout) ; la cheminée (Chimney : Mortared Stone, Brick) est contextuelle
sur le même type au-dessus / en dessous (Solo, Bottom, Middle, Top). `index.json` fait le lien `MaterialItem → Set` ; un
matériau absent de l'index se rend en cube de couleur. `fixtures/ranch-hewn-log.json` (import JSON du planner) montre
l'ensemble des formes Hewn Logs : maison sur pilotis, terrasse à barrières, chemin en planches, rampe, ponton.

**FarEast** (`FarEastLumberItem`) : les 67 formes au marteau du jeu + Stacked1-3 (liste `FAREAST_FORMS`) : 19 murs
`Wall_01..19` (01 uni, 02-19 à colombages), coins/T/X `_01..04`, colonnes, fenêtre, plafond, barrières `Fence*`,
escaliers `Stairs*`, toits à rives (`RoofEdge*`, `RoofPeak*`, `RoofUnderslope*`, `UnderInnerPeak`, `RoofCube`). Le
joueur choisit la variante et l'orientation (4 blocs tournés sans condition), les barrières, escaliers et toits se
raccordent par leurs voisins. Particularités : **miroir X** de l'import FBX d'Unity appliqué à ce set seulement
(voir « Repère et rotations ») ; les murs à colombages (`FarEast_Mat03.mat`) ont leur propre masque de
texture ; faute de bloc `Wall`, le matériau de la skin est celui du Cube (plâtre). `fixtures/fareast-showcase.json`
montre toutes les formes.

**Verre** (`GlassItem`, set `Glass`, dossier `Player Built Blocks/Glass`) : les 9 formes au marteau du jeu (`Glass.cs`) +
Stacked1-4 : `Cube` (vitre dans un cadre en bois : slot 0 bois « Simple » aux UV, slot 1 verre), `Window` (vitre mince
contextuelle : droite, coin, T, croix selon les voisins de catégorie Building), `ThinWallStraight`, `ThinWallCorner`,
`EdgeWall` (vitre contre un bord), `EdgeWallTurn` (le long de deux bords) tournées par le joueur, `FlatRoof` (vitre
horizontale au milieu), `ThinFloorTop` / `ThinFloorBottom` (en haut / en bas de la case), piles de vitres dans une caisse
(`Stacked1-4`, bois de `FurnitureTexture.mat`). Ces formes sont propres au set dans l'extracteur (`SET_FORMS`) : Brick a
aussi `ThinFloorTop/Bottom` (4 blocs tournés, là où le verre n'a qu'un bloc). Le mesh du cube vient du dossier Framed Glass. `TransparentBlock`
(builder d'Ashlar) n'est pas une forme et sort du calcul du préfixe. `fixtures/glass-showcase.json` montre toutes les formes.

**Brick** (`BrickItem`) : les 36 formes au marteau de `Brick.cs` + Stacked1-4 (+ Chimney). En plus des formes communes :
contreforts `Brace`, `BraceCorner`, `BraceTurn`, `SideBrace`, `SmallCornerBrace`, `UnderBrace*` (builders dans
`Brick Brace/`), pentes simples `BasicSlopeSide/Point` et retournées `UnderSlopeSide/Peak` (`importRotation` z = 180),
`ThinFloorTop/Bottom`, `ThinWallEdge`, `WindowEdge`, `WindowGrillesEdge`, `Aqueduct` (rigole contextuelle, même type)
et les rampes `RampA-D`, dont le blockset est rangé avec les routes (`Blocksets/Road Blocks/Brick Ramp Blocks.asset`,
`EXTRA_BLOCKSETS`, matériau `Brick Road.mat`) : 4 blocs tournés à 4 cas chacun (bords selon le même bloc de chaque côté,
aucun cas sans condition mais toutes les combinaisons couvertes). `Slope` (ancienne pente, pas au marteau) est ignoré.
Données du jeu reproduites telles quelles : `SideBrace` r2/r3 inversées, `UnderSlopePeak` r0 = r3.

**T4 : Corrugated Steel** (`CorrugatedSteelItem`, 27 formes au marteau + Stacked1-4) **et Reinforced Concrete**
(`ReinforcedConcreteItem`, 35 + Stacked1-4), une skin chacun. Communes aux deux : `Window` (leur seule fenêtre au marteau,
contextuelle), `DoubleWindow`, `Fence`, `FlatRoof` (rebord de 0,25 à 0,32 hors de la case sur les bords libres),
`RoadBarrier`, le toit intelligent, pentes simples et retournées. Acier : `FloatStairs*` ; sa barrière de route est rangée
avec les routes (`Road Blocks.asset`, `EXTRA_BLOCKSETS`, seuls les blocs au préfixe du set). Béton : `ThinColumn`,
`UnderStairs`, `BasicSlopeCorner/Turn`, `UnderSlopeCorner/Turn`, `HalfSlopeA/B`, `PeakSet`, `UnderPeakSet` ; ses toits
(pans, coin, faîte, `RoofCube`, `RoofPeakSet`) utilisent les builders du Composite Lumber avec son propre matériau
(`CL10 Reinforced Concrete Metal Roof.mat`), plus hauts que la case (0,31) ; son coin de toit a l'angle bas au sud-est en
r0, comme FarEast (`CornerRot`, `CORNER_R0`) ; sa colonne n'a ni solo ni sommet (bas, bas retourné `_X180`, milieu) ; sa
`DoubleWindow` vient d'Ashlar. Hors marteau, ignorés : `SlopeSide`, `ClippedFloatStairs*` (acier), les pièces en bouleau
du Composite Lumber du béton (`FullWall`, `WallTrim`, `WindowWall`, `SideFence`, `CladWall`, `Brace`, `UnderBrace`,
`RampA-D`, `FloatStairs`) et son ancienne `WindowGrilles` (`SET_EXCLUDED`). Les barrières de route (FBX 3ds Max)
portent une `GeometricRotation` de −90° en X que l'extracteur cuit dans le mesh, comme Unity.

**T5** : toutes les formes au marteau + Stacked1-4, sets `Ashlar Stone` (6 pierres, un bundle `AshlarStone`), `Composite
Lumber` (11 bois, un bundle par bois `Composite<Bois>Lumber`, option `--skin`), `Flat Steel`, `Framed Glass`.
- Ashlar : les formes du blockset `Ashlar <Pierre> Pediment Blocks` (frontons `RampA-D`, pentes, escaliers flottants,
  faîtes, toits CL) ont le même préfixe que la skin et des builders dans des sous-dossiers : les deux blocksets sont
  fusionnés en une skin. `Ashlar Stone` n'a pas d'item en jeu (`SKIN_EXCLUDED`). Nouvelle forme `FullWall` (mur plein).
  Cheminées : matériaux sous `Player Built Objects/Building Additions/Fireplace` (ajouté à l'index des guids).
- Composite Lumber : blockset `… Ramp Blocks` fusionné de même ; Hardwood et Softwood sans item (`SKIN_EXCLUDED`). Le
  béton, le Lumber et Ashlar empruntent des builders du dossier : un blockset n'est une skin que si la majorité de ses
  blocs a un builder du set. Nouvelles formes `WallTrim`, `CladWall`, `WindowWall`, `SideFence`, `FullWall`. `SideBrace`
  réutilise le mesh du `Brace` tourné de 90° en Z (`importRotation`).
- Flat Steel : nouvelle forme `WindowCorners` (poteau entre vitres en coin, T, croix ; catégorie `Window`), toit
  intelligent `Roof` (catégorie `MetalRoof`), pas de `Stairs` (le générateur pose `FloatStairs`), murs aux meshes numérotés.
- Framed Glass : verre au slot 0 des murs, au slot 1 ailleurs ; sous-matériaux `FramedGlassSteel…` (acier) et `Glass…`
  (verre) dans un ordre qui change d'un mesh à l'autre ; `PeakSet` / `UnderPeakSet` du blockset hors marteau, ignorés.
- Toits CL (pans, coin, faîte) pour les quatre, coin bas au sud-est en r0 comme le béton (`CornerRot`, `CORNER_R0`).

**Adobe** (`AdobeItem`, set `Adobe Brick`, bundle `AdobeBrick`) : les 30 formes au marteau d'`Adobe.cs` + Stacked1-3. Murs
arrondis posés tournés par le joueur (`WallMid`, `WallSolo`, `WallEnd`, `WallCorner`, `WallT`, `WallX` ; 4 blocs à
conditions, famille `rots` : bas, milieu, haut, seul selon le même mur dessus / dessous), parapets de toit plat (`RoofMid`,
`RoofSolo`, `RoofEnd`, `RoofCorner`, `RoofT`, `RoofX` ; `RoofFill` = dalle dans la moitié haute de la case), barrières et
escaliers aux noms de FarEast, `UnderSlopeSide`, `Ladder`. Un seul matériau (`Adobe_Mat.mat`, masque propre). Mesures des
meshes : mêmes conventions que FarEast (mur r0 le long de x, coin r0 bras ouest + nord, T r0 branche nord, escalier, fenêtre)
→ le générateur réutilise `FramedRing` / `Junctions`. `RoofCorner` est un angle de parapet : check_bundle ne teste le coin
de toit que dans les sets à `RoofSide`.
Décors : les parapets ont des `decorativeBuilders` (`Adobe_Roof_*_Dec`) : le jeu ajoute au mesh du bloc celui du premier cas
du décor qui s'applique (poutres qui sortent côté vide, selon les voisins pleins de part et d'autre ; le décor du T
embarque celui du X). L'extracteur (`decorate`, sets de `DECORATIVE_SETS` : Adobe, Garden Gravel) travaille sur les cas
compilés (rotations déroulées) : un cas par cas du décor compatible, mesh fondu (`base+décor`), conditions réunies, puis le
cas seul ; les entrées couvertes par une entrée plus haut sont retirées. La forme devient contextuelle (`rots`), sans
changement du rendu JS. Le cube du gravier y gagne son rebord en bois (`GardenGravelCube_Dec`, 10 cm au-dessus du bloc,
sur les bords sans voisin). Toit plat du
générateur en adobe : parapets `RoofMid` / `RoofCorner` sur les murs et `RoofFill` au même niveau (poutres dehors), muret
`WallMid` au-dessus si parapet.

**Routes** (dossier `Player Built Blocks/Roads Types/<set>`, blocksets `Blocksets/Road Blocks/`, cherchés pour ces sets
seulement) : `Asphalt Road` (`AsphaltConcreteItem`, 23 formes : `Cube` à bords, `RampA-D`, lignes peintes `WhiteLine`,
`WhiteDashLine`, `WhiteEdge` qui suivent leurs voisins, `WhiteEdgeRotate`, `TwoWhiteEdgeRotate`, `WhiteCube`,
`WhiteRampLine/DashLine/EdgeA-D`) ; la peinture est le canal « côté » du masque (`AsphaltW_Albedo`). `Stone Road`
(`StoneRoadItem`, `Cube` + `RampA-D`) : deux blocksets d'un seul bloc chacun, préfixe imposé (`SKIN_PREFIXES`). Les rampes
sans cas de repli couvrent toutes les combinaisons (comme Brick). Route en terre et Brick Road : pas d'item de bloc, non pris.

**Tapis** (set `Fabric Blocks`, dossier `Player Built Blocks/Fabric Blocks`) : un blockset pour 6 tissus, découpé par
`SKIN_PREFIXES` en `CottonCarpetItem`, `NylonCarpetItem`, `WoolCarpetItem` (les rideaux, `IgnoreRooms`, ne sont pas dans le
planner). Formes `Floor` (bordure en bois, slot 1), `SimpleFloor`, `FullWall`, `Cube`, `CanopyWindow` (auvent tourné, cas
dessus / dessous), Stacked1-4.

**Terrain** (`extract_terrain.py`, bundle `Terrain`) : terre et 13 roches concassées (T0, proposés par le planner). En jeu
ces blocs se lissent avec le terrain (builders « Blending », LOD, décorateurs) ; le bundle ne garde que la texture : aucune
forme, chaque bloc est le cube généré par le JS (dessus / dessous en `top`, côtés en `side`), teinte du `.mat` cuite.

**Tuyaux** (`extract_pipes.py`, set `Pipes`) : un blockset `Blocksets/Pipe Blocks.asset`, un builder et des meshes par
métal (`Player Built Blocks/Pipes/Pipe Builders/<Métal>Pipe.asset`), un matériau commun `PipeBlocks.mat` plaqué par
les UV. Une forme par métal (`CopperPipe`, `IronPipe`, `SteelPipe`), 25 cas × 4 rotations sur les 6 voisins de
face, prédicat `type:CopperPipe,PipeSlot` = voisin du même métal (matériau `CopperPipeItem`) ou prise d'un objet
(cellule `Solid` de son occupancy, `vox.pipeSlots`). Les meshes sont chiraux : l'extracteur applique le miroir X de
l'import Unity (sans lui, 32 masques sur 64 partent du mauvais côté). `skinForms` donne la forme d'une cellule de ce
matériau posée sans forme. Pour les pièces, un tuyau n'est pas un mur (le jeu le traverse) ; en jeu un tuyau traverse un mur ou une dalle qui reste en place : `evalOps` garde le bloc sous le tuyau
(`vox.under`, tuyau posé après le bloc), la vue 3D dessine les deux et les voisins du bloc le voient ; la case reste un
tuyau pour le 2D, les pièces et le coût.

## Comment le jeu décrit un bloc (ce que l'extracteur reproduit)

- **Blockset** `Blocks/Blocksets/Player Building Blocks/<Skin> Blocks.asset` : un bloc par forme et par rotation
  (`MortaredSandstoneRoofSide90`), chacun avec son **builder** (guid), son **matériau** (`Material`, plus `Materials[]`
  pour les sous-matériaux du mesh, ex. le verre) et sa **catégorie** (`Building`, `Terrain`, `Default`). Skins d'un set :
  les blocksets dont la majorité des blocs a un builder du dossier du set (sous-dossiers compris) ; deux blocksets au même
  préfixe sont une seule skin (Ashlar Pediment, Composite Ramp).
- **Builder** `.asset` : `usageCases[]` = mesh (FBX ou prefab), `importRotation.y`, conditions par voisin
  (`OffsetCondition.Offset`, 26 voisins) avec des règles `EqualsCategory` / `EqualsThisType` / … (`BlockRuleInternal.cs` ;
  « même type » = même bloc, donc même rotation pour une forme à 4 blocs tournés ; catégorie vide : jamais égale) ; premier cas dont
  toutes les conditions sont vraies ; `applyConditionsToAllRotations` décline le cas en 4 rotations. `importRotation` :
  y va dans la rotation de la forme, x et z (Euler ZXY du jeu, appliqués avant y, multiples de 90 : 180 pour les pièces
  retournées, 90 pour le `SideBrace` du Composite Lumber et les bords de vitres Flat Steel / Framed Glass) sont cuits dans
  le mesh (suffixe `_Z180`, `_X180`, `_Z90`…).
- **Trois familles de formes**, classées d'après les blocs du blockset : explicites (4 blocs tournés sans condition,
  le joueur choisit : Stairs, Roof*, fenêtre Brick, Ladder, DocksRampA…) → liste indexée par la rotation de l'op ;
  contextuelles (un bloc, sélection par les voisins : Wall, Floor, Column, WindowGrilles, cube du gravier, DocksColumn,
  Stacked1…) → cas compilés `{mesh, rot, conds:[[dx,dy,dz,attendu(,prédicat)]…]}` ; orientées et contextuelles
  (4 blocs tournés, chacun avec son builder à conditions : DocksPlatform, DocksFenceMid, DocksRamps…) → `rots`, un jeu
  de cas par rotation de l'op. Prédicats supportés : `category:<Cat>` (voisin non vide dont la forme a cette catégorie
  ; voxel de la maison = Building ; la catégorie d'une forme peut dépendre de sa rotation : `categories.Forme` est alors
  un tableau de 4, ex. DocksPlatform1/2), `sameType` (même forme et même matériau, rotation ignorée) et `solid`
  (voisin non vide ; `IsSolidType`/`NotSolidType`, dont le `ruleString` du jeu est ignoré). Les autres règles rendent
  le cas inutilisable : il est omis, sauf `NotEqualsType:WorldObject` (toits intelligents Mortared Stone, toits plats et
  toits CL des T4), tenu pour vrai : il est toujours couplé à un `NotSolid` sur le même voisin et n'écarte qu'un objet posé
  juste au-dessus, que le rendu ne voit pas comme un bloc.
- **Masque** : celui du `_MaskTex` du matériau du bloc de chaque forme (le plus souvent `BuildingMask.tga`) ; un même
  FBX sous deux masques donne deux meshes.
- **Plusieurs matériaux par bloc** (`Material` + `Materials[]`, un par slot FBX : poutres, plâtre et bois des murs
  FarEast) : les triangles du slot k ≥ 1 vont dans `slots[k − 1]` du mesh, chaque slot classé avec le masque de son
  matériau ; `slotMaterials` donne le matériau de chaque slot par skin et par forme, le JS dessine chaque slot dans le
  tampon de son matériau (slot sans matériau : celui de la forme). Sets déjà extraits sans ce découpage : inchangés.
- **FBX** : binaire 7.4, unités en cm (`UnitScaleFactor` 1, 100 = 1 bloc) ou en m (FarEast, `UnitScaleFactor` 100) ;
  recentrage par le `RotationPivot` du modèle ; modèle nommé `Collider` ignoré ; sous-matériau nommé `Glass…` ou `Solid
  Glass…` (pas `FramedGlassSteel…`), sinon slot dont le matériau du bloc a le shader du verre `Curved/Triplanar Obscure
  Glass` (vitres du set Glass, sans nom) →
  triangles translucides `glass`. Teinte du set, lue sur le matériau de ce slot (slot 0 = `Material`, i =
  `Materials[i−1]`) comme le fait le shader (`TRIPLANAR_FADE`) : moyenne de la texture du canal dominant de son `_MaskTex`
  (`PureRedMask` → `_TopTex`) × sa teinte (`_TopTint`), alpha = `_Color.a`, soit gris-bleu à 30 % pour tous les verres
  de fenêtre et le set Glass ; autre shader : `_Color` × moyenne de la `_MainTex`. Côté rendu, une vitre posée sur une
  face du cube est masquée contre un voisin plein, comme le `FaceRemover3000` du jeu (deux cubes de verre accolés).
- **Textures** : shader `TriplanarMasked` du client (`Client/Assets/Shaders/Curved/Triplanar/Triplanar.cginc`).
  Par pixel, le masque partagé `BuildingMask.tga` échantillonné aux UV du mesh choisit la texture : vert → `_SideTex`
  triplanaire, rouge → `_TopTex` triplanaire, bleu → `_DetailTex` plaquée par les UV du mesh (ardoise des toits, dessus
  des murs, cerclage du gravier). UV triplanaires = position (blocs) × `_TextureScale` (0,2 partout : un motif tous les
  5 blocs) + `_TextureOffset`. L'extracteur classe chaque sommet (`chan` 0/1/2, `uv` conservés pour le détail) et écrit
  un matériau par `.mat` rencontré (`materials`), le matériau du mur par skin (`skins`) et les exceptions par forme
  (`formMaterials`, ex. toits sur `<Skin> Roof.mat`). Textures absentes du checkout : repli sur la propriété suivante
  (`_MainTex`) ou sur `DETAIL_FALLBACK`. Les teintes `_SideTint` / `_TopTint` / `_DetailTint` multiplient la texture de
  leur canal dans le shader : une teinte non blanche est cuite dans une tuile d'atlas à part (acier ondulé : côté rosé,
  dessus gris, clôture bleu-gris, détail bleuté du toit) ; les matériaux de verre gardent leur teinte à part.
  Métallicité : le planner ne la rend pas, alors qu'un métal paraît en jeu plus sombre que son albédo (tôle du toit du
  béton blanchâtre sinon). Avec le shader `TriplanarMaskedMetalness`, la tuile est multipliée par 1 − `METAL_DARKEN` ×
  métal (R de `_SideMetalness` / `_TopMetalness` / `_DetailMetalness`) ; map absente ou noire (bois, pierre) : inchangée.
  `METAL_DARKEN` = 0,5, à caler en jeu.

## Repère et rotations

Planner : x est, y sud, z haut ; Unity : X, Y haut, Z (main gauche). `--axis x,z,y` par défaut, `--base-offset
Forme=deg` pour un décalage par forme. Un +90° Unity autour de Y vaut −90° dans le plan (`rotSign` −1).
**Le jeu tourne en sens inverse du planner** : pour la rotation r = 0..3 d'un bloc, le côté bas d'un pan de toit ou
d'un escalier regarde est, nord, ouest, sud. Le générateur C# (`BuildingGenerator.LowToward`) et le rendu partagent
cette convention ; `fixtures/calibration.json` + son README servent à la confirmer en jeu. En cas d'écart en jeu,
corriger `--axis` / `--base-offset` dans l'extracteur, pas le générateur.

**Miroir X** : Unity inverse X à l'import d'un FBX ; `extract_forms.py` l'applique à tous les sets. Preuve par la
cohérence des meshes contextuels avec leurs règles (sans → avec miroir) : dalles FarEast 292 → 316/316, bras des murs
Mortared / Brick / Lumber 72 → 88/88 et Hewn 88 → 104/112 (le reste : bouts de rondins aux angles), faîte en L
`MS_Roof2Way` vers ses deux voisins, coins du toit intelligent 0 → 12/12, avant-toit des `RoofSupportCube` vers le voisin
vide 0 → 12/12. Pans de toit et escaliers n'en changent pas (pente le long de y) ; les coins tournent d'un quart :
angle bas de `RoofCorner` r0 au sud-ouest pour `MS_RoofCorner` (Hewn, Mortared, Brick, Lumber), au sud-est en FarEast
(meshes propres), d'où `BuildingGenerator.CornerRot` par matériau. `check_bundle.py` contrôle les bras des murs, le côté
bas de RoofSide et l'angle bas de RoofCorner.

## Format du bundle (version 4)

```
index.json   { sets: { Set: { json, atlas } }, materials: { MaterialItem: Set }, objects?: {…},
               formGroups?, formMeta?, materialForms? }   // formes du marteau, voir plus bas
<Set>.json   { set, version: 4, tile: <px d'une tuile>, calibration, categories: { Forme: catégorie | [cat × 4 rotations] },
               meshes: { nom: { pos, nrm, idx, glass?, slots?: [idx du slot FBX 1, 2…], uv?, chan? } },
               forms: { Forme: [ {mesh, rot} × 4 ]  |  { bitMeans, cases: [ {mesh, rot, conds} ] }
                               |  { rots: [ { bitMeans, cases } × 4 ] } },
               materials: [ { side, top, detail, scale, offset } ],   // indices de tuiles de l'atlas (null = côté)
               skins: { MaterialItem: matIdx }, formMaterials: { MaterialItem: { Forme: matIdx } },
               slotMaterials?: { MaterialItem: { Forme: [matIdx des slots 1, 2… | null] } },
               glass?: [r, g, b, a] }
<Set>.webp   atlas carré, tuiles de `tile` px en ligne-major (2048² jusqu'à 4 textures, 4096² au-delà)
```

Côté JS : `evalOps` porte `shapes` (Uint16, `(indexFORMS+1)·4 + rot`, 0 = cube plein) ; `formsPick` choisit le mesh
(forme absente du set → cas du Cube du set → cube généré) ; `v3dMesh` encode le canal dans `aUv` (−2 côté, −3 dessus,
uv ≥ 0 détail, icônes = normale nulle) ; un tampon GL par `Set:matIdx`, textures côté/dessus/détail en unités 1-3,
verre dessiné en dernier en mélange alpha. Les formes sont des cubes pleins pour les pièces et le coût : seuls les rendus
(3D, icônes et côté bas du plan 2D) lisent `Form`/`Rot` des ops.

## Formes du marteau (données de la palette de formes)

```
python Scripts/forms/make_form_meta.py      # défauts : --eco ../Eco, --out ecocraft/wwwroot/assets/forms
```

Ajoute à `index.json` : `formGroups` (groupes du jeu dans l'ordre de `BlockFormGroup/*.cs`, ceux qu'un matériau utilise),
`formMeta: { Forme: { group, order } }` (`TechTree/data/BlockFormType/*.json`) et `materialForms: { MaterialItem: [ { name,
rotatable } ] }` = les `IsForm` du matériau (`AutoGen/Forms/*.cs`) ∩ les formes du bundle de son set, triées par groupe puis
ordre du jeu, sans Stacked1-4 ; `rotatable` = liste de 4 ou `rots` (le joueur choisit), sinon la forme suit ses voisins.
Terrain et tuyaux n'ont pas de forme au marteau : absents de `materialForms` (cube seul). Le script signale un `IsForm` sans
forme dans le bundle (aucun à ce jour ; la barrière `Fence` du Lumber manquait, absente de `SET_FORMS`). Les extracteurs
relisent `index.json` et gardent ces clés, mais une liste peut vieillir : **relancer `make_form_meta.py` après chaque extraction d'un set**. Côté site : `ClientMaterial.Forms` et
`ClientCatalog.FormGroups` (`BuildingPlannerCatalogService`), libellés `BuildingPlanner.Form.<Forme>` /
`BuildingPlanner.FormGroup.<Groupe>`, icônes `eco-icons/<Forme>Form.png` / `<Groupe>FormGroup.png`.

## Objets (meubles, portes, tables d'artisanat)

`extract_objects.py` produit des bundles `kind: objects` (même dossier, même `index.json`, clé `objects: { Item: Set }`),
un set par famille pour tenir dans un atlas (≤ 64 textures) : 25 manifestes `objects/*.json`, 722 objets sur les 765 du
catalogue — `ObjectsAdobe`, `ObjectsHewn`, `ObjectsMortared`, `ObjectsLumber`, `ObjectsCrafting`, `ObjectsAshlar` (166, les
premiers livrés), puis `ObjectsCrafting2`, `ObjectsIndustry`, `ObjectsCivic`, `ObjectsCulture`, `ObjectsDoors`,
`ObjectsStorage`, `ObjectsUtility`, `ObjectsVehicles`, `ObjectsHome`, `ObjectsHomeDecor`, `ObjectsHomeLighting`,
`ObjectsOutdoor`, `ObjectsFountains`, `ObjectsFrames`, `ObjectsShelves`, `ObjectsComposite`, `ObjectsSignsWood`,
`ObjectsSignsStone`, `ObjectsSignsMisc` (le champ `note` de chaque manifeste dit ce qui est écarté et pourquoi).

```
python Scripts/forms/extract_objects.py --content "<checkout>/Eco/Content/Art" --manifest Scripts/forms/objects/ObjectsHewn.json --out ecocraft/wwwroot/assets/forms
python Scripts/forms/check_objects.py ecocraft/wwwroot/assets/forms/ObjectsHewn.json         # parts, emprise vs cases, sol, RESULT OK
python Scripts/forms/fixtures/make_furniture_showcase.py ecocraft/wwwroot/assets/forms > Scripts/forms/fixtures/furniture-showcase.json
```

- **Format** : `{ set, version: 1, kind: 'objects', tile, meshes: { nom: { pos, nrm, idx, uv, chan: 2, shade? } }, materials: [{ side, top: null,
  detail }], objects: { Item: { parts: [{ mesh, material, glass? }], rot: [degrés planner × 4], cells: [[x, y, z]…] } } }`. Le JS dessine les
  parts une fois, à la case d'ancrage de l'objet (`formsObject`, `view3dBuild`) ; `cells` n'est qu'informatif (l'occupancy vient du
  catalogue serveur), `rot[r]` = angle planner de la rotation r de l'objet (Quaternion Unity 90·r autour de Y).
  `glass: [r, g, b, a]` (0..1) marque une part en verre : matériau transparent (`_Mode` 2 fondu ou 3 transparent, ou
  `RenderType: Transparent`), rgb = `_Color` × albédo moyen de sa texture (`_Color` seul sans texture), a = `_Color.a`. Sa
  tuile reste l'albédo (teinté), ou une tuile unie de sa couleur sans texture. Le JS dessine une part d'alpha < 0,95 en vitre
  translucide unie de cette teinte (tampon de verre par teinte, après l'opaque) ; au-delà (grille de l'IndustrialBarge, a = 1), texturée.
  Un verre d'alpha < 0,05 est écarté (`PictureFrameGlass_Mat` des cadres).
- **Lecture d'un prefab** : MeshFilter et SkinnedMeshRenderer → FBX + `.mat` → `_MainTex` ; `PrefabInstance` résolue
  récursivement — variante d'un autre prefab (Hardwood/Softwood, par pierre : matériaux en override `m_Materials.Array.data[i]`)
  ou FBX instancié (tables : modèles du FBX, matériaux du `externalObjects` du `.meta` ; override `m_Mesh` : mesh d'un autre FBX,
  animaux empaillés). Seuls les overrides de transform (position, rotation, échelle) de la **racine** d'une instance la déplacent
  (racine = source du Transform « stripped » rangé dans un `m_Children`, ou cible de `m_LocalRotation.w` pour une variante) ; ceux
  d'un nœud interne d'un FBX instancié remplacent le transform local de ce nœud, ses enfants suivent (petite marmite, cube
  tournant, échelle 100 des racines du moulin à vent ramenée à 1, recycleur, marteau du marteau-pilon). Une géométrie
  partagée par plusieurs nœuds (pales du moulin à vent) donne un renderer par nœud.
  Renderers LOD ≥ 1, `collider`/`collision`, bâche `foundation_cloth` (fondations, masquée une fois fondées) et GameObjects
  inactifs ignorés — y compris sous un parent inactif (porte Fir) et les GameObjects d'un FBX retirés (`m_RemovedGameObjects`) ou
  désactivés par l'instance (broyeur du tri des déchets, couvercles des poubelles, toiles du chevalet) ; une instance dont le
  GameObject racine est inactif est ignorée. Ces GameObjects sont retrouvés par leur **fileID haché** (`fileIdsGeneration: 2`) :
  xxHash64 de `Type:GameObject->//RootNode/root/<chemin du nœud>0`, chemin sans la racine d'un fichier à racine unique ; la racine
  est `//RootNode/root0` (919132149155446097). Transform (cible des overrides de nœuds internes) et composants : xxHash64 de
  `Type:Transform->//RootNode/root/<chemin du nœud>/Transform0` (racine `//RootNode/root/Transform0`, −8679921383154817045 ;
  MeshRenderer : `…/MeshRenderer0`), vérifié sur le cube tournant et le moulin à vent.
  Mesh d'un renderer : par le fileID de son `m_Mesh`
  (table `internalIDToNameTable` du `.meta`, type 43, ou xxHash64 de `Type:Mesh-><nom>0`), sinon le modèle d'un GameObject frère
  au même fileID nommé comme lui (roue 1 des charrettes), sinon par le nom du GameObject (suffixes `.NNN`, `_N`, `(N)` tolérés),
  sinon le premier. Un mesh est découpé par slot de matériau, **dans l'ordre des sous-meshes Unity** (première apparition de
  l'index de matériau dans les polygones, puis les index inutilisés : bouée, poubelle en acier) ; deux parts identiques (torche
  allumée et éteinte superposées) n'en font qu'une. Échelle nulle : renderer ignoré. FBX ASCII (outil du chevalet) : instance
  ignorée. Variante dont l'override de matériau vise un renderer d'un FBX instancié dans sa source (tables par pierre ou par bois :
  cible = XOR de l'instance et d'un id interne haché du FBX, non recalculable) : appliqué par slot à tous les modèles de ce FBX si
  la source n'en instancie qu'un. Override de matériau d'un FBX instancié directement dans le prefab : sa cible est le fileID
  haché du renderer d'un nœud (`fbx_renderer_ids` : « Type:SkinnedMeshRenderer-><nœud>0 » ou forme par chemin), appliqué à ce
  nœud seul (pont de la barge en bois en `Barge 2`, bordages en `Barge 1`) ; cible non retrouvée : à tous les modèles. Occupancy : boîte `size`/`occupancyOffset` du WorldObject (taille négative : cases
  offset + size .. offset − 1, banderoles, comme `WorldObjectOccupancyAutoGen.cs`) plus les `WorldObjectOccupancyObject` (portes,
  prises de tuyau).
- **Texture en tableau** (shader peinture `Curved/TintableTextureArrayShader`, matériau `Workbench_01_CommonMat.mat` de 19
  meubles) : son `_MainTex` `Common_Workbenches_01_Albedo.png` (8192²) est importé en Texture2DArray (`textureShape: 4`,
  flipbook 4 × 4 lu de gauche à droite, de haut en bas : 0 bois clair, 1 bois foncé, 2 planches Hewn, 3 planche de détails…).
  Le shader prend la tranche `UV1.x × 20` (2ᵉ couche d'UV du FBX, entiers exacts) et l'échantillonne aux UV0 : l'extracteur
  découpe le mesh par tranche (une part et une tuile d'atlas par tranche utilisée). Même shader : couleur × (1 − alpha de la
  couleur de sommet), cuit dans `shade` (0-255 par sommet, le JS multiplie la couleur du sommet). Peinture du joueur
  (`_PaintedAmount`, 0 par défaut) ignorée. u est décalé d'un entier par mesh pour rester ≥ 0 : le rendu lit un u < −0,5
  comme un sommet sans texture (`check_objects.py` le refuse).
- **Matériaux** : `.mat` lu sur son document Material (certains commencent par un MonoBehaviour URP : bateaux). CurvedStandard /
  Standard multiplient `_MainTex` par `_Color` : une teinte non blanche est cuite dans la tuile (panneaux et porte Joshua :
  texture Hardwood teintée brun). Décalcomanies `TransparentCutout` (matériau nommé `*Decal*` : vis, autocollants, ornement de la
  cloche) opaques sur moins de la moitié de leur surface (alpha échantillonné aux UV) : écartées, le rendu sans alpha en ferait des
  aplats blancs ; les surfaces ajourées (grillage, vannerie, filet, tapis de bain) restent.
- **Repère** : **Unity négative X à l'import d'un FBX** (main droite → main gauche) et l'occupancy des prefabs est calée sur le mesh
  importé (table : mesh brut en +X, cases X −1..0) ; l'extracteur applique ce miroir (et inverse le sens des triangles, rétabli
  par une échelle négative), le « geometric transform » du nœud, le recalage sur son `RotationPivot` (ramené dans le repère des
  sommets : pivot / `Lcl Scaling`, établi de charpentier et évier à 0,01) et l'échelle du `.meta` (`useFileScale`,
  `globalScale`). **Pose du nœud FBX** (chaîne `Lcl Translation` + `RotationOffset` + pivot / `PreRotation` / `Lcl Rotation` /
  `Lcl Scaling`) : elle va sur le GameObject importé du nœud, **pas dans le mesh** ; elle place donc les nœuds d'un FBX instancié
  (tables, machines), jamais un MeshFilter du prefab, placé par sa seule chaîne de Transform (position, rotation, échelle). Preuve :
  les BoxCollider posés sur le GameObject même du MeshFilter (calés par Unity sur le mesh importé), 72 renderers dont le nœud a une
  pose ou une échelle : « sans pose ni `Lcl Scaling` » est la meilleure hypothèse partout où le collider suit le mesh (pompe à
  balancier : 0,000 contre 0,13 à 4,1 en cuisant rotation ou pose, `Lcl Scaling` 2,54 non cuit ; vase −90° X sous un GameObject
  identité : 0,000 contre 0,275 ; banderoles `Lcl Scaling` 0,01 : 0,000 contre 5,9). Exceptions : un GameObject au transform local
  nul (position, rotation et échelle ; l'enseigne du panneau de magasin, sous son os à l'échelle 6,58, n'en est pas un)
  d'un FBX dont un Animator du prefab joue un clip (grande porte en acier ondulé : le clip remet le nœud à sa pose), ou d'un
  mesh skinné dont les os ne sont pas dans le prefab, reçoit la pose du nœud. Un fichier dont `FrontAxisSign` et `CoordAxisSign`
  valent −1 reçoit un demi-tour vertical sur ses racines (conversion d'axes de l'import). **Bake Axis Conversion**
  (`bakeAxisConversion: 1` au `.meta` : étagères, livres, pots…) : Unity cuit la conversion dans les sommets, un demi-tour autour
  de Y de plus (sauf fichier −1/−1, où il annule celui des racines) ; preuve : BoxCollider et `LODGroup.m_LocalReferencePoint` des
  35 étagères (dos côté −Z, contre le mur). Chaîne de Transform du prefab : racine ignorée (le jeu la pose au centre de la case
  d'ancrage ; racine d'un FBX à nœud unique instancié : position et rotation écrasées par l'instance). **Mesh skinné** dont les os
  sont dans le prefab : posé comme Unity, sommet par sommet, moyenne pondérée sur les clusters du FBX de os (monde du prefab) ·
  `TransformLink`⁻¹ · pose de liaison du mesh (`BindPose`) · geometric transform (raffinerie, four de boulangerie, cloche :
  centre du LODGroup à 0,01 près) ; sans os connus, l'extracteur essaie avec et sans pose du nœud et garde celle
  qui rentre dans l'occupancy (note « occupancy fit ») ; FBX instancié dont un Animator du prefab joue un clip unique (boucle
  permanente) : os à la première image du clip (entrée `clipAnimations` du `.meta` à l'`internalID` du `m_Motion`, première
  clé de chaque courbe de son take ; cerf-volant). Fichier « bake » : conversion d'axes complète sur ses racines (`UpAxis`,
  `FrontAxis`, `CoordAxis` et signes ramenés sur +Y, +Z, +X ; cerf-volant, haut +X : quart de tour autour de Z) et demi-tour
  des vecteurs d'os ; sans bake, les fichiers au haut +X (dessalinisateur, filtre à eau) sont justes sans conversion. `extract_forms.py` applique le même miroir aux formes de blocs.
- **Limites** : pas d'animation ni de texte (panneaux : planche seule, police TMP ignorée), matériaux non texturés et non
  transparents ignorés (eau du chantier naval, carte du bureau immobilier). Matériau absent du checkout : tuile unie si le
  manifeste la donne (`plain` : guid du `.mat` → rgb 0..1 ; bassin de jardin, teintes de son icône), sinon part ignorée.
  Débords admis par `check_objects.py` (`OVERFLOW`, en blocs : au-delà, erreur) : flèche de la grue (10,3), cerf-volant en
  l'air (9), rampe immergée du chantier naval et socle du moulin à vent, sur la case d'ancrage absente de l'occupancy du
  prefab (1). Barge en bois : mesh et occupancy du prefab (x −6..0), la liste serveur (x 0..6) en est le miroir X, corrigée
  côté site. HewnDresser a une instance imbriquée absente du checkout (sans conséquence). FancyHewnDoor, ZatakuTable et ShojiDoor
  (porte coulissante FarEast 4 × 2) sont des objets payants (`PaidItemEmbeddedList.cs`) : les GUID de leurs prefabs (groupes
  Addressables `IAP` pour ShojiDoorObject / ZatakuTableObject, `Objects-WorldObjects` pour FancyHewnDoorObject) ne sont dans aucun
  `.meta` du checkout (`Art/IAP` non livré), seule leur icône marketplace y est ; le planner les dessine en boîte à l'icône. Applique à
  torche en bois : torche sous un GameObject inactif, non dessinée.
- **Générateur IA** : élément `furniture` du programme (`type`, `x`, `y` = case nord-ouest de l'emprise tournée, `baseZ`, `rot`) ;
  l'enum des types = objets du catalogue présents dans `index.json` (`BuildingPlannerPage.RenderableFurniture`), leurs emprises
  sont listées dans la description du tableau ; `BuildingGenerator.PlaceFurniture` retrouve l'ancre, vérifie l'intérieur d'un volume,
  les cloisons et les chevauchements, pose un `PlanObject` (`f1…`, Z null = au sol).

## Limites connues

- FarEast : le générateur pose les murs en `Wall_01..19` (champ `wallForm` des volumes et cloisons, `Wall_01` par
  défaut), chaque côté tourné, `WallCorner` aux angles, `WallT` / `WallX` aux jonctions des cloisons (style assorti,
  `BuildingGenerator.Walls.cs`), `StairsMid` et `Window` ; les autres formes FarEast passent par l'élément `blocks`. Les
  motifs chiraux (Wall_13, Wall_18…) ne sont pas orientés vers l'extérieur. Les avant-toits, limons d'escalier et le
  haut d'échelle débordent de la case jusqu'à 0,4 (géométrie du jeu, tolérée par check_bundle).
- Garden Gravel : `_SideTex` et `_DetailTex` absentes du checkout (`Art/IAP` refusé par le serveur SVN) → repli
  pierre de route + `WoodDetail_Hardwood_Albedo.png`. Sa seule variante, Zen Garden (`ZenGardenItem`, objet payant, hors
  palette), n'a ni builders ni matériaux dans le checkout : non extraite. Le rebord en bois prend le détail de repli (bois
  clair) : en jeu il est brun-rouge.
- Décors (Adobe, gravier) : fondus à l'extraction ; un décor sans mesh est un cas « rien » qui arrête la recherche, comme en jeu.
- Hewn Logs : Slope n'est pas extrait (pas au marteau). Le bloc DocksFenceEndCap n'est qu'un
  capuchon de poteau (le jeu le pose au bout d'une barrière) ; DocksPillarBeam occupe le haut de sa cellule, posé sur
  une pile de DocksPillar il laisse un jour. Le générateur atteint ces formes par l'élément `blocks` du programme
  architectural (une boîte, une forme, une rotation) ; les rotations de ces formes n'ont pas été contrôlées en jeu.
- Pas de normal map, de détail secondaire, d'éclairage du jeu. Le contrôle des rotations en jeu reste à faire.

## Ajouter un set ou une forme

Un set : une commande `extract_forms.py --set "<Dossier>"`, puis `check_bundle.py`, puis `docker cp`. Une forme :
l'ajouter à `EXPLICIT_FORMS`, `CONTEXT_FORMS` ou `DOCK_FORMS` ici (la famille se déduit des blocs), à `FORMS` dans le
JS et à `BuildingGenerator.Forms` (même ordre : le code de forme d'une cellule en dépend), puis décider où le
générateur la pose. Un nouveau prédicat de voisinage : `SUPPORTED_PREDICATES` ici et `formsNeighbor` dans le JS (`type:` des tuyaux :
`extract_pipes.py`).
