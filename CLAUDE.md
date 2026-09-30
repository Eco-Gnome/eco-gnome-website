# eco-gnome-website

Site Blazor Server (`ecocraft/`) pour les joueurs d'Eco ; le building planner (`ecocraft/BuildingPlanner/`,
`ecocraft/wwwroot/js/building-planner.js`) est la partie la plus dense.

## Règles de travail

- **Build et déploiement local en Docker** : `docker compose build app && docker compose up -d --force-recreate app`
  (http://localhost:8080). Pas de `dotnet build` du site sur la machine ; pour du C# pur, un projet console à part
  (ex. `Scripts/gen-bench`). Après un changement de JS, rappeler le Ctrl+Shift+R (script versionné par la release).
- **Git** : branches `feature/…` sur `develop`, rebase seulement, jamais de merge ; commits en anglais sur une ligne,
  sans trailer ; jamais de push sans accord explicite ; jamais `git add -A` (d'autres agents peuvent travailler dans le
  même arbre). `ecocraft/wwwroot/assets/` est gitignoré : les fichiers de langue s'ajoutent avec `git add -f`, les
  bundles de formes (assets SLG) ne se commitent jamais.
- **Tests** : `Scripts/gen-bench` (générateur, `dotnet run`), `Scripts/forms/tests` (fonctions JS pures, Node),
  `Scripts/forms/check_bundle.py`. Le test en application est fait par Romain : livrer, vérifier une fois, rendre la main.
- **Assets du jeu** : `../Eco/Content/Art` (SVN, lecture seule) ; documentation de la chaîne formes/textures dans
  `Scripts/forms/README.md`.
- **Formes 3D d'un set** : avant de le dire complet, comparer ses formes à `IsForm` dans
  `../Eco/Server/Mods/__core__/AutoGen/Forms/<Matériau>.cs`, regarder les `Materials[]` de ses blocs (un matériau par
  slot FBX) et le `_MaskTex` de chaque `.mat`, puis passer `check_bundle.py` (côté des dalles = test du miroir X).
  La sémantique des règles de voisinage est dans `../Eco/Client/Assets/EcoEngine/Internal/EcoModKitInternal/VoxelEngine/
  BlockRuleInternal.cs`. Juger le rendu sur une vitrine importée (`Scripts/forms/fixtures/make_*_showcase.py`), toutes
  les ops dans la grille.
- **Palette de formes** : les formes proposées pour chaque matériau viennent de `Scripts/forms/make_form_meta.py`
  (`index.json`), à relancer après chaque extraction d'un set.
- **Pièces** : une pièce est une poche d'air fermée par des blocs, comme en jeu (`RoomChecker.cs`) ; plus de sol ni de
  plafond automatiques, tout le bâti est dans les ops (un plan v3 est migré à l'ouverture). Dessiner une pièce : outil
  Pièce (touche P, mode Structure).
- **IA** : pas de clé globale ; une clé par fournisseur (Anthropic, Mistral : `AiProviders`) est celle du serveur, saisie
  par un admin dans « Gestion du serveur » (colonne JSON `Server.AiKeysJson`, chaque clé chiffrée Data Protection, relue
  et révoquée par son seul propriétaire, qui peut ouvrir la génération aux joueurs) ; fournisseur actif (`Server.AiProvider`)
  et modèle choisis par n'importe quel admin, changer de fournisseur garde les autres clés.
  Le tout n'apparaît que si un super-admin a activé la génération IA du serveur (`Server.IsAiGenerationEnabled`).
  « Modifier » marche sur tout plan non vide : `PlanDescriber` (texte du plan) → outil `submit_plan_edit` → `PlanEditor`
  (suppressions, changements, creux, puis `BuildingGenerator.Generate(…, baseDoc)` pour les ajouts), testé dans gen-bench.
