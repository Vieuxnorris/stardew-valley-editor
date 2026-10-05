# MODLOG — Valley Editor

Journal de travail. Roadmap : [docs/ROADMAP.md](docs/ROADMAP.md).

## Environnement (2026-10-05)
- Jeu : Stardew Valley 1.6.15 build 24356 — `G:\Steam\steamapps\common\Stardew Valley` (MonoGame, .NET 8 x64, pas d'anti-cheat)
- SMAPI 4.5.2 (installé via Vortex) ; Mods gérés par Vortex (`__folder_managed_by_vortex`)
- Mods notables : SpaceCore, ContentPatcher 2.9.1, JsonAssets, CJBCheatsMenu 1.40, GMCM, PPJA*, MoreOres, SkullCavernElevator, Automate
- Saves : `%APPDATA%\StardewValley\Saves\` — log SMAPI : `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`
- Outils : .NET SDK 8.0.425, ilspycmd, Node 24.11 / npm 11.6
- Décompilé (hors repo) : `%USERPROFILE%\stardew-decomp`

## Route
Mod C# SMAPI + serveur HTTP local (127.0.0.1, token) + UI web Vite/TS/Preact servie par le mod.
Règles data-driven via Content API (`AssetRequested`, priorité Late) ; règles codées en dur via Harmony.
Raison : SMAPI déjà installé, code du jeu lisible en C#, live sans quitter le jeu.

## Backups / restauration
- 2026-10-05 : `%USERPROFILE%\.universal-modder\backups\stardew-saves\20261005-162702.zip` (toutes les saves, 33 fichiers)
- Restaurer : `um backup restore stardew-saves` (ou dézipper dans `%APPDATA%\StardewValley\Saves`)

## Journal
- 2026-10-05 — Phase 0 : backup, décompilation, MODLOG.

## Save de labo
- `Saves\Lab_448403492` = copie de `God_448403491`, `uniqueIDForThisGame` passé à 448403492 et `farmName` à « Lab » (dans les 2 fichiers).
- Pourquoi changer l'ID : le dossier d'écriture est `<préfixe du fichier>_<uniqueIDForThisGame>` (SaveGame.cs:448, SaveGame.cs:695). Avec le même ID, la save de labo écraserait l'originale.
- Copie intacte : `%USERPROFILE%\.universal-modder\lab\Lab_448403492.pristine`. Restaurer = supprimer le dossier de save et recopier la copie intacte.

## Faits moteur (décompilé 1.6.15)
- **Pause quand la fenêtre n'a pas le focus** : `Game1.Update` (Game1.cs:3902) fait `return` si la fenêtre est inactive et que `options.pauseWhenOutOfFocus` vaut true. C'est le cas chez Julien (`startup_preferences`). Le navigateur aura le focus pendant l'édition. À VÉRIFIER en phase 1 : si l'`UpdateTicked` de SMAPI continue de tourner, notre dispatcher fonctionne quand même.
- **Cultures** : `Crop.phaseDays` (NetIntList) est rempli depuis `CropData.DaysInPhase` + 99999 dans le constructeur `Crop(seedId, …)` (Crop.cs:264). Les cultures déjà plantées gardent leurs phases : il faut les recalculer à la main.
- **Mine** : `MineShaft.populateLevel()` (privée, l.1321) tire `stoneChance` = 10–30 %, `monsterChance`, `itemChance` = 0.0025 et `gemStoneChance` = 0.003, puis appelle `adjustLevelChances(ref …)` (privée, l.1231). Le minerai vient de `getAppropriateOre(Vector2)` (publique, l.1733) et `tryToAddOreClumps()` (publique, l.1810), les cailloux de `createLitterObject(...)` (privée, l.4351). Point de patch idéal pour la densité : postfix sur `adjustLevelChances`, en multipliant les `ref`.
- 2026-10-05 — Phase 1 VALIDÉE en jeu (save Lab). `/api/status` et `/api/player` répondent, l'or est modifié en live. Sans jeton : 401. Avec une autre Origin : 403. Le jeu est en plein écran fenêtré : les modifs restent visibles en live même quand le navigateur a le focus, donc le dispatcher sur `UpdateTicked` tourne bien.
- Gotcha : `HttpListener` sur `http://localhost:<port>/` ne demande pas les droits admin, contrairement à `127.0.0.1`. Le déploiement par ModBuildConfig échoue tant que le jeu tourne (DLL verrouillée) : compiler avec `-p:EnableModDeploy=false`, puis redéployer jeu fermé.
- Gotcha : ModBuildConfig 4.4.0 ne référence pas Newtonsoft.Json. Il faut ajouter `<Reference HintPath="$(GamePath)\smapi-internal\Newtonsoft.Json.dll" Private="False">`.
- Gotcha : `NetIntHashSet` n'a pas d'`AddRange` et `NetList` n'a pas de `RemoveAll` : boucler.
- Faits joueur : `gainExperience` remplit `newLevels` (les écrans de montée de niveau s'affichent au coucher). `LevelUpMenu.RevalidateHealth(farmer)` recalcule les PV max (combat + Fighter/Defender). Les noix dorées sont dans `Game1.netWorldState.Value.GoldenWalnuts`, l'XP de maîtrise dans `Game1.stats["MasteryExp"]`.
- 2026-10-05 — Phase 2 codée (joueur complet, inventaire, catalogue avec icônes PNG). En attente du test en jeu.
- 2026-10-05 ~16:50 — **INCIDENT** : `Mods/` ne contient plus que ValleyEditor, le zip Quantium et `vortex.deployment.json`. SMAPI n'a chargé qu'un seul mod. Dernière modification de `Mods/` : 16:34:06.
  - Vortex déploie en `symlink_activator`, avec le staging dans `%APPDATA%\Vortex\stardewvalley\mods`. Ce staging est intact (69 mods, 61 Mo) : seuls les liens ont disparu. Réparation : Vortex → Deploy.
  - Cause : purge Vortex volontaire de Julien (confirmé). Ce n'est pas un bug de notre côté.
  - Impact : seule la save Lab a été sauvée sans mods (16:45). Les autres saves n'ont pas été modifiées depuis septembre. Après le redéploiement, restaurer Lab depuis la copie intacte.
- Constat catalogue : 2 446 items, tous du jeu de base (normal, aucun mod chargé). La détection du mod d'origine reste à tester avec les mods.
- 2026-10-05 — Phase 2 VALIDÉE par Julien en jeu (joueur, inventaire, catalogue, icônes). Reste à faire : tester l'attribution des items à leur mod une fois Vortex redéployé.
- 2026-10-05 17:04 — Phase 3 codée et déployée : WorldDomain (date, heure, météo par contexte, téléportation), ProgressionDomain (déblocages par mail flags, mines, salles du Centre communautaire, musée « tout donner », flags mail/events), QuestsDomain. La save Lab a été restaurée depuis la copie intacte. Les mods Vortex sont toujours purgés.
- Faits monde : la météo est stockée par contexte dans `netWorldState.LocationWeather` et recopiée dans les statiques `Game1.isRaining` etc. pour « Default » (`Game1.ApplyWeatherForNewDay`). La météo de demain se règle comme un totem de pluie (`netWorldState.WeatherForTomorrow` + `Game1.weatherForTomorrow`). Pour le jour, recalculer aussi `stats.DaysPlayed` (formule de DebugCommands.Day). L'ascenseur lit `netWorldState.LowestMineLevel`.
- 2026-10-05 17:20 — Phase 3 VALIDÉE par Julien en jeu (date/saison/heure, météo, téléportation et mines, musée/CC/quêtes/flags). Log propre. Pas encore vu : la restauration visuelle d'une salle du CC le lendemain. Les mods Vortex sont toujours purgés (attribution des items à leur mod non testée).
- 2026-10-05 — Phase 4 codée et déployée : NpcsDomain (cœurs/points plafonnés comme `Farmer.changeFriendship` : `(GetMaximumHeartsForCharacter+1)*250-1`, fréquentation, cadeaux, a parlé aujourd'hui, actions groupées), portraits via `/api/portraits/{name}`. Mariage, divorce et enfants reportés.
- 2026-10-05 — Phase 4 testée via l'API (31 villageois, plafonds 8/10 corrects, portraits PNG 64×64). Phase 5 codée (pas encore déployée, le jeu tourne) :
  - Cultures : `Data/Crops` est édité en priorité Late. `RuleMath.ScalePhases` répartit le total multiplié entre les phases ; des phases de 0 jour sont OK car `Crop.newDay` les saute. Pour les cultures déjà plantées : `Crop.ResetPhaseDays()` puis `HoeDirt.applySpeedIncreases(player)` (engrais conservé).
  - Machines : `Data/Machines` OutputRules `MinutesUntilReady`/`DaysUntilReady`. Arbres sauvages : `Data/WildTrees` GrowthChance. Arbres fruitiers : sur `DayStarted`, retrancher les jours en plus de `daysUntilMature`, puis `FruitTree.DaysUntilMatureToGrowthStage`.
  - Mine : postfix Harmony sur `MineShaft.adjustLevelChances` (`ref` stoneChance/monsterChance/gemStoneChance) et sur `populateLevel` (convertit pierres et minerai avec `getAppropriateOre` et `mineRandom`). En vanilla, environ 2,9 % des pierres sont du minerai (`createLitterObject`).
  - Règles par save : `helper.Data.Read/WriteSaveData("rules")` sur SaveLoaded/Saving, remise à zéro sur ReturnedToTitle. 22 tests xUnit sur RuleMath (projet mod/ValleyEditor.Tests, qui lie le .cs sans référencer le jeu).
- 2026-10-05 — Pack « Mode OP » ajouté aux règles par save (CheatsService) :
  - Pêche instantanée : `fishingBiteAccumulator = timeUntilFishingBite`, puis ferrage façon AutoHookEnchantment (`DoFunction` sur la canne), puis `BobberBar.distanceFromCatching = 1.5`. Prise parfaite (`bar.perfect`), trésor (`bar.treasure` et `treasureCaught`).
  - Butin : `Player.InventoryChanged` quand aucun menu n'est ouvert (donc pas les achats ni les coffres). On ignore les 2 ticks qui suivent une écriture de l'éditeur (`EditorState.LastWriteTick`) et nos propres changements de pile (ConditionalWeakTable). Postfix sur `GameLocation.monsterDrop` pour les tirages supplémentaires (avec garde anti-récursion), postfix sur `Object.sellToStorePrice`.
  - Joueur : PV et énergie remis au max à chaque tick, heure figée via `Game1.gameTimeInterval = 0`, chance via `team.sharedDailyLuck = 0.125` sur DayStarted, buff ENDLESS invisible (Speed, MagneticRadius, LuckLevel).
  - Mine : échelle garantie via `createLadderDown` dans le postfix de populateLevel.
- 2026-10-05 18:01 — Pack pêche et butin déployé (non testé en jeu) :
  - Prises : prefix Harmony sur `FishingRod.pullFishFromWater` (`ref` fishSize/fishQuality/numCaught ; la prise parfaite remonte encore la qualité comme en vanilla). Taille max = champ 4 de `Data/Fish` (sauf les entrées « trap »). Poisson forcé : postfix sur `GameLocation.getFish`.
  - Coffre au trésor : `Display.MenuChanged` avec `ItemGrabMenu.source == source_fishingChest (3)`, on modifie `ItemsToGrabMenu.actualInventory` (grille par défaut de 36 cases → plafond).
  - Tables de pêche : édition de `Data/Locations` → `Fish` (chance par Id, entrées retirées, ajouts avec Precedence -100 et IgnoreFishDataRequirements). Monstres : champ 6 de `Data/Monsters` (« id chance … », IDs d'objets non qualifiés). Les deux sont capturés tels que les autres mods les ont laissés.
  - Collection : `farmer.fishCaught["(O)id"] = [count, maxSize]`. Commandes spéciales : `team.specialOrders` (objectifs `SetCount(GetMaxCount())` puis `CheckCompletion()`) et `team.completedSpecialOrders`.
- 2026-10-05 18:10 — BUG (signalé par Julien) : la table de butin des monstres ne s'appliquait pas. Le jeu en français charge `Data/Monsters.fr-FR`, et `InvalidateCache("Data/Monsters")` ne touche pas les variantes localisées (« Invalidated 0 cache entries »). Correctif : `InvalidateCache(a => a.NameWithoutLocale.IsEquivalentTo(...))`.
- Gotcha : `CamelCasePropertyNamesContractResolver` met aussi les clés de dictionnaire en camelCase (« green Slime »). Remplacé par `CamelCaseNamingStrategy { ProcessDictionaryKeys = false }`.
- 2026-10-05 — BUG (Julien) : les quantités du coffre au trésor restaient à 1 avec le multiplicateur ×3 (log propre, cause exacte non identifiée avec `Display.MenuChanged`). Remplacé par un postfix Harmony sur `FishingRod.openTreasureMenuEndFunction` (qui se termine en assignant l'ItemGrabMenu source 3), avec une trace dans le log à chaque coffre.
- Demande : pouvoir pêcher un poisson n'importe quand. Ajout, par entrée de `Data/Locations` Fish, d'une saison forcée (`any` → Season=null), de « sans restriction » (Condition=null, IgnoreFishDataRequirements=true, CatchLimit=-1) et de « prioritaire » (Precedence=-50).
- 2026-10-05 — Précision de Julien : par « quantités du coffre », il voulait dire le **nombre d'objets**. La boucle vanilla `while (random <= p) { p *= golden ? 0.6f : 0.4f; … }` donne le plus souvent un seul objet. Transpiler sur `openTreasureMenuEndFunction` : les 2 `ldc.r4 0.4/0.6` (vérifiés dans l'IL, juste avant `mul`) passent par `TreasureDecay()`, qui renvoie 1 tant que le minimum de tirages n'est pas atteint (compteur remis à zéro par un prefix). Les piles identiques sont fusionnées, et au-delà de 36 cases le surplus est lâché au sol (`createItemDebris`).
- Carte du monde : `Data/WorldMap` (régions → BaseTexture + MapAreas avec PixelArea, Tooltips, WorldPositions.LocationName). Image composée côté serveur (base + textures de zone dont `GameStateQuery.CheckConditions` passe, mélange alpha prémultiplié), zones cliquables en % dans l'UI.
- Icônes de monstres : `Characters/Monsters/<nom>`, première frame. Par défaut AnimatedSprite fait 16×24, avec une table de tailles relevée dans les classes Monsters (32×32 BigSlime/Serpent/DinoMonster/Leaper/Shooter, 16×16 Bug/MetalHead/…, 16×32 Mummy/Skeleton/ShadowBrute), puis rognage sur les pixels visibles.

- 2026-10-05 — BUG (Julien) : « Prix de vente » refusait 1. `<input type=number min=0.01 step=0.5>` : la grille du pas part de `min`, donc seuls 0.01, 0.51, 1.01… sont valides. Garder `min` multiple de `step`.
- Carte : une MapArea (Town, Beach, Farm…) contient plusieurs Tooltips (un par bâtiment) et plusieurs WorldPositions, et un tooltip n'a pas de lieu. Chaque tooltip est associé à la position dont la MapPixelArea (vide → PixelArea de la zone) le recouvre le mieux (IoU). En cas d'égalité : nom == id du tooltip ou de la zone, puis lieu extérieur.
- Slimes (Green Slime, Frost Jelly, Sludge) : même planche grise `Green Slime`, teintée au dessin (constructeur GreenSlime). Shadow Guy et Skeleton Warrior n'ont pas de planche propre (alias Shadow Brute / Skeleton).
- Demande : construction gratuite. Prefix sur `CarpenterMenu.DoesFarmerHaveEnoughResourcesToBuild` / `ConsumeResources`. Les améliorations de maison sont codées en dur dans `GameLocation.houseUpgradeAccept` (privée, niveaux 0-2), remplacée par un prefix sans coût.
- Construction instantanée : `Building.FinishConstruction()` une fois aucun menu ouvert. La maison reste à 1 nuit, car `Farmer.dayupdate` déplace les meubles et change la carte.
- Artisanat gratuit : prefix sur `CraftingRecipe.doesFarmerHaveIngredientsInInventory` / `consumeIngredients` (couvre aussi la cuisine).
- 2026-10-05 — Historique et annulation. Le chemin de la requête suit le code jusqu'au thread du jeu via `AsyncLocal` (`RequestContext`). Les domaines enregistrent un undo avec `UndoCapture.Remember` (ThreadStatic, le premier appel gagne) avant de modifier quoi que ce soit. Pour cloner les objets, on passe par `SaveSerializer.GetSerializer(typeof(Item))`, parce que `getOne()` perd les stats des armes et les accessoires des cannes. L'historique est vidé à chaque sauvegarde : un undo de la veille écraserait toute la journée.
- Machines : pour terminer, `MinutesUntilReady = 0` puis `minutesElapsed(0)` (le même chemin que l'horloge). Les machines `OnlyCompleteOvernight` passent ensuite en `readyForHarvest` à la main. Le mode « instantané » (multiplicateur 0) met les données à 10 min, et un tick toutes les 30 frames termine les machines concernées.
- Animaux : réglages par espèce dans `Data/FarmAnimals` (DaysToProduce, DaysToMature, luxe garanti via `DeluxeProduceMinimumFriendship=0` et `DeluxeProduceCareDivisor≈0`). « Produire maintenant » : les produits déposés la nuit vont dans le sac, ceux récoltés à l'outil passent par `currentProduce`.
- `um publish check` : les 10 « FAIL game file copied verbatim » sont de faux positifs. Ce sont nos propres fichiers comparés à la copie déployée dans `Mods/ValleyEditor`, qui est dans le dossier du jeu.
- 2026-10-05 — Sortie des machines gardée à chaque lot : postfix sur `Object.OutputMachine` (`__result && !probe`), qui réapplique un `OutputOverride` (objet, quantité, qualité ; null = valeur du jeu). La règle d'une machine précise va dans son `modData` (`Julien.ValleyEditor/Output`, sauvegardé avec l'objet), celle d'un type de machine dans les règles. Portée infinie : prefix sur `withinRadiusOfPlayer` / `tileWithinRadiusOfPlayer`, limité à `tileRadius <= 2` et au joueur, parce qu'avec un rayon plus large ces fonctions servent aux comportements des PNJ (se tourner vers le fermier). Poser n'importe où : `isPlacementForbiddenHere` est forcé à false, et un postfix sur `playerCanPlaceItemHere` n'autorise que les cases libres (ni objet, ni meuble, ni bâtiment, ni culture). `UndoCapture` enchaîne désormais toutes les captures et les rejoue en ordre inverse.
