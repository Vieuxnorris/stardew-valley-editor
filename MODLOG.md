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
- 2026-10-05 : `C:\Users\Julien\.universal-modder\backups\stardew-saves\20261005-162702.zip` (toutes les saves, 33 fichiers)
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
