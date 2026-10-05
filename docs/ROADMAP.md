# Roadmap — « Valley Editor » : éditeur live complet pour Stardew Valley

## Contexte

Julien veut un **éditeur complet** pour Stardew Valley, plus puissant que CJB Cheats Menu (déjà installé).
Il doit pouvoir modifier en direct l'état d'une partie (joueur, inventaire, PNJ, monde, ferme) **et les règles
du jeu** (temps de pousse des cultures, minerais par étage de mine, etc.).

Décisions prises avec l'utilisateur :
- **Forme** : hybride live. Un mod SMAPI expose l'état du jeu via HTTP local, et une UI web s'ouvre dans le navigateur.
- **Périmètre** : joueur et inventaire, relations et PNJ, monde et temps, ferme et bâtiments, règles de jeu.
- **Jeu** : solo uniquement.
- **Démarche** : un MVP rapide, puis des itérations domaine par domaine.
- **Règles** : stockées **par sauvegarde**.
- **UI** : web local servi par le mod, en **FR et EN** (i18n).

## Repérage (fait)

| Élément | Valeur |
|---|---|
| Jeu | Stardew Valley **1.6.15** build 24356, Steam, `G:\Steam\steamapps\common\Stardew Valley` |
| Moteur | MonoGame / .NET 8 x64, **aucun anti-cheat** |
| Loader | **SMAPI 4.5.2** (installé via Vortex), plus de 80 mods dont SpaceCore, ContentPatcher 2.9.1, JsonAssets, CJBCheatsMenu 1.40, GenericModConfigMenu, PPJA, MoreOres |
| Saves | `%APPDATA%\StardewValley\Saves\` (9 slots XML, par ex. `God_448403491`) |
| Log | `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` |
| Outils | .NET SDK 8.0.425, ilspycmd, Node 24 / npm 11, git |
| Repo | `stardew valley cheat engine` (vide, branche `master`) |

**Route choisie** : un mod C# SMAPI, avec deux leviers.
- **Content API** (`AssetRequested`) pour les règles qui viennent des données : `Data/Crops`, `Data/FruitTrees`, `Data/Machines`…
- **Harmony** pour les règles codées en dur, comme la génération des étages de mine.

C'est la route la moins coûteuse, car SMAPI est déjà en place et tout le code du jeu est en C# lisible.

## Architecture

```
┌──────────── Stardew Valley + SMAPI ────────────┐        ┌──── Navigateur ────┐
│ ValleyEditor (mod C#)                          │        │ UI web (Vite + TS) │
│  ├─ WebServer   HttpListener 127.0.0.1:47800   │◄─HTTP─►│ onglets : Joueur,  │
│  │   + token d'accès, sert /wwwroot + /api     │  JSON  │ Inventaire, PNJ,   │
│  ├─ GameThreadDispatcher (file → UpdateTicked) │        │ Monde, Ferme,      │
│  ├─ Domains/  Player, Inventory, Items, Npcs,  │        │ Règles · FR/EN     │
│  │            World, Farm (lecture/écriture)   │        └────────────────────┘
│  ├─ Rules/    RulesData (par save) +           │
│  │            AssetEditors + HarmonyPatches    │
│  └─ Sprites   rendu d'icônes d'items → PNG     │
└────────────────────────────────────────────────┘
```

Principes clés :
- **Thread du jeu** : le serveur HTTP ne touche jamais l'état du jeu directement. Chaque requête est mise en
  file (`ConcurrentQueue` + `TaskCompletionSource`), puis exécutée dans `GameLoop.UpdateTicked`.
- **Sécurité** : le serveur écoute uniquement sur `127.0.0.1`. Un token aléatoire est exigé dans l'URL ou en
  en-tête, et l'en-tête `Origin` est vérifié pour qu'une autre page web ne puisse pas piloter le jeu. La
  commande console `editor` ouvre le navigateur avec ce token.
- **Items moddés** : on passe par `ItemRegistry` (IDs qualifiés `(O)…`, `(BC)…`, etc.), donc les items JA, CP
  et PPJA sont gérés sans code spécifique. Les icônes viennent de `ParsedItemData.GetTexture()` et
  `GetSourceRect()`, sont rendues en PNG sur le thread du jeu et mises en cache.
- **Règles par save** : on charge avec `helper.Data.ReadSaveData<RulesData>("rules")` sur `SaveLoaded` et on
  écrit sur `Saving`. Après un changement de règle, `InvalidateCache` recharge l'asset concerné. Les éditions
  d'assets sont en priorité `Late`, pour passer **après** les autres mods (PPJA, MoreOres…).
- **Persistance** : comme dans le jeu de base, les modifs restent en mémoire jusqu'à la sauvegarde nocturne.
  L'UI l'indique avec un bandeau « non sauvegardé ».

Arborescence prévue :
```
/mod/ValleyEditor/        ValleyEditor.csproj (net6.0, Pathoschild.Stardew.ModBuildConfig, Lib.Harmony via SMAPI)
  ModEntry.cs, manifest.json, i18n/{default,fr}.json
  Server/  Domains/  Rules/  Patches/  Sprites/
/mod/ValleyEditor.Tests/  xUnit pour la logique pure (calcul des règles, DTO, validation)
/web/                     Vite + TypeScript + Preact ; `npm run build` → mod/ValleyEditor/wwwroot
MODLOG.md  README.md  docs/ROADMAP.md  .gitignore (bin/obj/node_modules/wwwroot généré, aucun fichier du jeu)
```
Le décompilé du jeu va dans `%USERPROFILE%\stardew-decomp`, **hors du repo**.

## Phases

### Phase 0 — Labo et fondations (≈ ½ journée)
1. Créer `MODLOG.md` (chemins, versions, décisions) et le `.gitignore`.
2. Faire un backup de toutes les saves : `um backup create "%APPDATA%/StardewValley/Saves" --name stardew-saves`.
3. Créer une **save de labo** en copiant `God_448403491` vers un slot `Lab_…`, et garder une copie intacte à
   restaurer avant chaque test.
4. Décompiler `Stardew Valley.dll` avec `ilspycmd` vers `~/stardew-decomp`. Noter les noms exacts dans MODLOG :
   `Farmer`, `Crop.phaseDays`, `HoeDirt`, `MineShaft.populateLevel` / `chooseStoneType` / `getAppropriateOre`,
   `Game1.player.friendshipData`, `Utility.ForEachLocation`, `Game1.timeOfDay`, `Game1.weatherForTomorrow`…
5. Vérifier que le jeu continue de tourner (`UpdateTicked`) quand la fenêtre n'a pas le focus (option
   « pause when unfocused »), sinon l'API répond seulement quand le jeu est au premier plan.

### Phase 1 — Tranche verticale (≈ 1 journée)
Objectif : prouver toute la chaîne avec **une seule** donnée, l'argent.
- Squelette du mod : `ModEntry`, `WebServer`, `GameThreadDispatcher`, la commande `editor` et le token.
- `GET /api/player` et `PATCH /api/player {money}`.
- UI minimale servie par le mod : un champ « Or » et un bouton Appliquer.
- **Terminé quand** : je change l'or dans le navigateur, le HUD du jeu se met à jour, le log SMAPI est propre
  et une capture d'écran le confirme.

### Phase 2 — MVP v0.1 : Joueur et inventaire (≈ 2–3 jours)
- **Joueur** : or, Qi gems, golden walnuts, PV/énergie (actuels et max), niveaux et XP des 5 compétences, mastery,
  professions, vitesse, nom et ferme.
- **Inventaire** : grille des 36 cases, avec édition de la quantité et de la qualité, suppression et
  réorganisation, plus la taille du sac.
- **Catalogue d'items** : recherche plein texte avec filtres par type et par mod d'origine, icônes, et
  « Ajouter à l'inventaire » (quantité et qualité). Les items moddés sont inclus.
- **UI** : onglets, i18n FR/EN, bandeau « non sauvegardé », messages d'erreur.
- **Terminé quand** : je spawn un item vanilla et un item PPJA, je modifie une compétence, puis je dors pour
  sauvegarder. Après rechargement, tout est conservé.

### Phase 3 — Monde et temps (≈ 2 jours)
- Date (jour, saison, année), heure, météo du jour et du lendemain, et téléportation du joueur vers un lieu.
- Déblocages (mail flags et `eventsSeen`) avec des **préréglages lisibles** : objets du portefeuille spécial,
  bus, Ginger Island, clé du crâne, ascenseur de mine…
- Quêtes actives, Community Center (bundles et salles) / Joja, dons au musée.

### Phase 4 — Relations et PNJ (≈ 1–2 jours)
- Liste des PNJ avec portrait : points d'amitié et cœurs, cadeaux de la semaine et du jour, « a parlé
  aujourd'hui », statut (fréquente / marié).
- Événements de cœur vus et rejoués.
- Changement de conjoint ou divorce : **reporté**, car risqué (état partagé entre la maison, les enfants et les flags).

### Phase 5 — Règles de jeu par save (≈ 3–4 jours)
Un écran « Règles » liste chaque réglage avec sa valeur vanilla, sa valeur actuelle et un bouton reset.

| Règle | Mécanisme |
|---|---|
| Temps de pousse des cultures (multiplicateur global et override par culture) | `Data/Crops.DaysInPhase` via AssetRequested, plus l'action « appliquer aux cultures déjà plantées » (recalcul de `Crop.phaseDays`) |
| Arbres fruitiers et arbres sauvages | `Data/FruitTrees.DaysUntilMature` / `Data/WildTrees` |
| Durée de traitement des machines | `Data/Machines` (`MinutesUntilReady`) |
| Minerais par étage de mine (densité, type par tranche d'étages, minerais rares) | Harmony sur la génération de `MineShaft` (cible exacte confirmée en phase 0) |
| Chance d'échelle, densité de monstres | Harmony sur `MineShaft` |
| Difficulté de pêche, prix de vente | Harmony / `Data/Objects` |

- Tests unitaires du calcul des règles (multiplicateurs, arrondis, minimum de 1 jour par phase).
- **Terminé quand** : avec ×0,5 sur les cultures, un panais planté pousse en 2 jours. Avec « minerais ×3 »,
  l'étage 50 de la mine montre visiblement plus de minerai (captures avant/après). Une autre save n'est pas
  affectée.

### Phase 6 — Ferme et bâtiments (≈ 3–4 jours)
- **Cultures** : tout arroser, faire pousser ou mûrir instantanément, choisir le stade, retirer les cultures
  mortes, sur la ferme, la serre ou l'île.
- **Animaux** : liste, amitié, humeur, âge, produit du jour, renommage.
- **Bâtiments** : finir une construction ou une amélioration immédiatement, et monter le niveau de la maison,
  des étables et des poulaillers.
- **Coffres** : liste des coffres de tous les lieux, avec édition de leur contenu (réutilise la grille d'inventaire).
- Vue carte interactive de la ferme (rendu des tuiles) : **optionnelle, en fin de phase**. Une interface en
  listes vient d'abord.
- **Ajouté en cours de route** : animal de compagnie (nom, amitié, caresse du jour, gamelle) dans l'onglet
  Animaux. Hors tuiles, la phase est livrée : le stade d'une culture se règle via « Mûrir », il n'y a pas de
  stade au choix.

### Phase 6b — Collections et objets spéciaux (≈ ½ journée) — demandée par Julien
- **Collections** : les pages du menu (objets expédiés, poissons, artefacts, minéraux, cuisine) et l'artisanat.
  Pour chacune, la progression (x/y) et un bouton « Compléter », plus « Tout compléter ». Les artefacts et
  minéraux sont comptés par le jeu via les dons au musée, donc on les dépose dans le musée.
- **Objets spéciaux et pouvoirs** : la page du portefeuille, lue depuis `Data/Powers`. Elle inclut les livres
  de pouvoir 1.6 et les entrées des mods. Chaque entrée s'obtient ou se retire, et « Tout débloquer » est
  disponible. Le déblocage passe par la condition de l'entrée (`PLAYER_HAS_MAIL`, `PLAYER_STAT`). Les autres
  conditions sont affichées en lecture seule.
- Les succès Steam sont volontairement **exclus** : ils sont définitifs sur le compte.

### Phase 6c — Éditeur d'items (≈ 2–3 jours) — demandée par Julien
Il s'ouvre en cliquant un objet de l'inventaire ou d'un coffre. On reprend le panneau quantité/qualité et on
l'étend selon le type de l'objet :

| Type | Champs éditables |
|---|---|
| Tous | quantité, qualité, prix de vente (`Object.Price`), « objet de quête » / ne peut pas être jeté |
| Armes (`MeleeWeapon`, `Slingshot`) | dégâts min/max, vitesse, chance et multiplicateur de critique, recul, précision, défense, zone d'effet ; enchantements (innés de la Forge, puis Galaxy Soul, Infinity…), nombre de forges, apparence (« transmog ») |
| Outils | niveau d'amélioration (cuivre → iridium), enchantements (Auto-Hook, Efficace, Rapide, Généreux, Reaching…), contenance de l'arrosoir ; canne à pêche : appât et matériel attachés |
| Anneaux | fusion de deux anneaux (`CombinedRing`) |
| Vêtements et chapeaux | couleur / teinture |
| Produits artisanaux | ingrédient d'origine (vin de *X*, confiture de *Y* : `preservedParentSheetIndex`) |

- Les valeurs vanilla sont affichées à côté de chaque champ, avec un bouton reset.
- Les bornes sont validées côté serveur. Les champs dont la valeur est recalculée par le jeu sont signalés
  dans l'UI : par exemple, les stats d'une arme reviennent aux données si elle est rechargée depuis `Data/Weapons`.
- **Point à vérifier en phase 0 de cette étape** : quels champs survivent à la sauvegarde XML et au
  rechargement. On teste avec la save de labo : dormir, recharger, comparer.

### Phase 7 — Finition et publication (≈ 2 jours)
- Historique des modifications et **annulation** de la dernière action.
- Config (port, ouverture auto du navigateur) et intégration GMCM (déjà installé).
- Compléter l'i18n, écrire le README avec les étapes d'installation, lancer `um publish check` (aucun fichier
  du jeu ni décompilé), préparer le paquet pour Nexus s'il est souhaité, avec une vidéo démo de 30 s.
- Note de terrain dans la base de connaissances (skill share-field-notes), avec l'accord de Julien.

Le MVP utilisable correspond aux phases 0 à 2. Les phases suivantes s'enchaînent dans l'ordre, et
chacune est livrable seule.

## Risques et points d'attention
- **Conflits de mods** : PPJA, MoreOres, SkullCavernElevator, Industrialization… modifient les mêmes
  données. On applique nos éditions en priorité `Late` et on teste avec la vraie liste de mods.
- **Vortex** gère le dossier `Mods`. Notre mod est déployé directement par ModBuildConfig dans
  `Mods/ValleyEditor` (dossier non géré par Vortex).
- **Saves corrompues** : on fait un backup avant chaque session, on écrit uniquement via l'API du jeu (jamais
  dans le XML) et on valide les bornes des valeurs (niveaux 0–10, qualité 0/1/2/4…).
- **Pas de sauvegarde en cours de journée** dans le jeu de base : on ne fait pas de « forcer sauvegarde » en
  MVP, et on l'explique dans l'UI.
- **Circuit breaker** : si un même échec se répète 3 fois, on s'arrête, on documente dans MODLOG et on
  change d'approche ou on consulte Julien.

## Vérification (à chaque phase)
1. `dotnet build` déploie le mod. Les tests xUnit passent (`dotnet test`).
2. `npm run build` génère `wwwroot`.
3. Lancer le jeu via SMAPI (`um win launch`), charger la **save de labo** et vérifier dans
   `SMAPI-latest.txt` que le mod est chargé et qu'il n'y a pas d'erreur.
4. Appeler l'API avec `curl` (avec le token), puis vérifier dans le jeu avec `um win shot --scale 0.33`.
5. Dormir pour sauvegarder, recharger et vérifier que les modifs sont conservées. Restaurer la save de labo
   intacte avant le test suivant.
6. Faire un commit git à chaque étape qui fonctionne.
