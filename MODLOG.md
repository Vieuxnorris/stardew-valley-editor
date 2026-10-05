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
