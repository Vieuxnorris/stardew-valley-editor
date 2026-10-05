---
kind: game
title: 'Valley Editor: a live save and rules editor for Stardew Valley in the browser'
game: Stardew Valley
games_also: []
game_version: '1.6.15 build 24356, Steam, SMAPI 4.5.2'
platform: windows
engine: xna-fna
route: managed-patch
tools: ["SMAPI 4.5.2", "Pathoschild.Stardew.ModBuildConfig 4.4.0", "Harmony (via SMAPI)", "ilspycmd", ".NET SDK 8.0.425", "Vite 8 + TypeScript + Preact", "xUnit"]
anti_cheat: 'none; single-player only by design'
status: working
agents:
- Claude Code (Opus 5.5)
humans: []
date: '2026-10-05'
links: []
tags: [save-editor, live-editing, http-api, web-ui, harmony, transpiler, content-api, smapi, rules, cheats, world-map, map-rendering, undo]
---
# Valley Editor: a live save and rules editor for Stardew Valley in the browser

> A SMAPI mod that runs a token-protected HTTP API on `localhost` and serves a web UI. The UI edits a running
> single-player game live:
> - player, inventory and any item's stats;
> - chests, villagers, date, time and weather, with teleport from the world map;
> - a rendered, clickable farm map with building interiors, machines and animals;
> - collections and wallet powers, fishing tables and monster loot;
> - per-save "rules": crop and machine speed, mine ore density, OP mode.
>
> Data-driven rules go through SMAPI's Content API and hard-coded behaviour through Harmony (prefix, postfix and
> one transpiler). The human validated phases 1–3 in game. Later features were checked through the API against
> the running game, or only compiled; see Verification.

## Setup
- **Game**: Stardew Valley 1.6.15, build 24356, Steam, Windows 11. MonoGame on .NET 6; no anti-cheat.
- **Loader**: SMAPI 4.5.2, installed by Vortex in symlink mode. The mod deploys to `Mods/ValleyEditor`, outside
  Vortex's management.
- **Build tools**: .NET SDK 8.0.425; the mod targets `net6.0` with `Pathoschild.Stardew.ModBuildConfig` 4.4.0
  and `EnableHarmony`.
- **UI**: Node 24 / npm 11 with Vite 8, TypeScript and Preact. The build writes into
  `mod/ValleyEditor/wwwroot` with stable (unhashed) file names.
- **Decompiled code**: `ilspycmd` output kept outside the repo (`~/stardew-decomp`), including the
  `StardewValley.GameData` assembly.
- **Lab save**: a copy of a real save with a new `uniqueIDForThisGame` (see Gotcha 1), plus a pristine copy to
  restore before tests.

## Route and why
`managed-patch`, combined with the loader API. SMAPI was already installed and the game is readable C#. Three
levers:
1. **SMAPI Content API** (`AssetRequested`, `AssetEditPriority.Late`) for everything data-driven:
   `Data/Crops`, `Data/Machines`, `Data/WildTrees`, `Data/Locations` (fish), `Data/Monsters` (drops),
   `Data/FarmAnimals`. Late priority runs after content packs, so multipliers also scale modded content.
2. **Harmony** for hard-coded logic:
   - mine generation, fishing catch and treasure chest;
   - build costs, house upgrades and crafting ingredients;
   - machine output, reach and placement checks.
3. **Direct game-state writes** on the game thread for live edits (money, items, friendship, weather…).

A plain cheat menu (like CJB Cheats Menu) was the alternative. A browser UI was chosen because:
- it has room for big tables (crops, fish, drops), item catalogs with icons and rendered maps;
- it edits while the game keeps running, in borderless windowed mode.

## How the game works (what we had to learn)
**Threading and the API**
- Never touch game state from `HttpListener` threads. Queue a closure in a `ConcurrentQueue` and drain it in
  `GameLoop.UpdateTicked`.
- `Game1.Update` returns early when the window is unfocused and `options.pauseWhenOutOfFocus` is set. SMAPI's
  `UpdateTicked` kept running in borderless windowed mode during the test.

**Saves and per-save data**
- The save folder is `<name prefix>_<uniqueIDForThisGame>`.
- Rules live in `helper.Data.Read/WriteSaveData("rules")`, written on `Saving`.
- Per-object settings live in `modData`, which is saved with the object.

**Content edits and locales**
- The game loads localized variants: a French game loads `Data/Monsters.fr-FR`.
- Invalidate with a predicate on `NameWithoutLocale`, not with the asset name.

**Crops**
- `Crop.phaseDays` is copied from `CropData.DaysInPhase` when the crop is created, so planted crops keep their
  phases.
- To change them: `Crop.ResetPhaseDays()`, then `HoeDirt.applySpeedIncreases(player)`, which keeps fertilizer.

**Mines**
- `MineShaft.populateLevel` and the private `adjustLevelChances(ref stoneChance, ref monsterChance, ref itemChance, ref gemStoneChance)`.
- A postfix multiplying those `ref`s changes density.
- Ore comes from `getAppropriateOre` and `tryToAddOreClumps`.

**Fishing**
- `FishingRod.pullFishFromWater` has `ref` size, quality and count: a prefix sets them.
- Forced fish: postfix on `GameLocation.getFish`.
- Treasure chest: built in `FishingRod.openTreasureMenuEndFunction`, which rolls items in a loop whose
  continuation chance decays by ×0.4 (×0.6 for golden). A transpiler routes those two float constants through a
  function returning 1 until a minimum roll count is reached.

**Machines (1.6, data-driven)**
- The output is created in `Object.OutputMachine(…, probe, …)`; postfix it to change every new batch.
- To finish one now: `MinutesUntilReady = 0`, then `minutesElapsed(0)`, the same path as the clock. Machines with
  `OnlyCompleteOvernight` need `readyForHarvest` set by hand.

**Buildings**
- Robin's menu: `CarpenterMenu.DoesFarmerHaveEnoughResourcesToBuild` and `ConsumeResources`.
- Farmhouse upgrades are hard-coded in the private `GameLocation.houseUpgradeAccept`.
- `Building.FinishConstruction()` completes builds and upgrades. An upgrade path is
  `upgradeName` + `daysUntilUpgrade = 1` + `FinishConstruction()`; upgrade targets are the `Data/Buildings`
  entries whose `BuildingToUpgrade` is the current type.
- The farmhouse itself is rebuilt overnight in `Farmer.dayupdate`, which moves furniture and swaps the map.

**Animals and pets**
- `FarmAnimalData.DaysToProduce`, `DaysToMature` and the deluxe formula (friendship ≥ `DeluxeProduceMinimumFriendship`
  and a roll against `(friendship + happiness) / DeluxeProduceCareDivisor`).
- Pets are an `NPC` subclass (`StardewValley.Characters.Pet`) with `friendshipTowardFarmer` (max 1000),
  `lastPetDay` and a `PetBowl` building.

**Wallet and collections**
- "Special Items & Powers" is `Data/Powers`: a `UnlockedCondition` game state query per entry. Most are
  `PLAYER_HAS_MAIL`, `PLAYER_STAT` (power books) or `PLAYER_HAS_SEEN_EVENT` (Bear's Knowledge 2120303, Spring
  Onion Mastery 3910979).
- The collections page counts artifacts and minerals by museum donation, not by "found".

**World map**
- `Data/WorldMap`: regions with base textures and map areas.
- One area (Town) has many tooltips (one per building) and many world positions. Tooltips carry no location:
  match each one to the position whose `MapPixelArea` overlaps it most.

**Rendering a location to PNG**
- Read tiles from the xTile layers `Back*`, `Buildings*`, then objects, furniture, trees, buildings and
  characters sorted by bottom Y, then `Front*` and `AlwaysFront*`.
- Use frame 0 of `AnimatedTile`. Load a tile sheet with `Game1.content.Load<Texture2D>(sheet.ImageSource)`.
- Furniture top-left is its bounding box bottom minus the source rect height.
- Game textures are premultiplied alpha: un-premultiply when encoding PNG.

**Exits between locations**
- Exits are `location.warps`, `location.doors`, and also tile properties: `TouchAction Warp <loc> x y` on Back
  (the farmhouse cellar stairs, set by `FarmHouse.updateCellarWarps`) and `Action Warp x y <loc>`,
  `LockedDoorWarp`, `MagicWarp`, `Warp<Place>` on Buildings.

**Deep-copying items**
- `Item.getOne()` doesn't copy weapon stats (min/max damage, speed…) or rod attachments. For a true clone,
  round-trip through `SaveSerializer.GetSerializer(typeof(Item))`.

## Build steps
1. Create a lab save: copy a save folder and change `uniqueIDForThisGame` and the folder/file names in both XML
   files. Keep a pristine copy.
2. Run `ilspycmd` on `Stardew Valley.dll` and `StardewValley.GameData.dll`, outside the repo.
3. Mod project (`net6.0`, ModBuildConfig, `EnableHarmony`). Add a reference to
   `$(GamePath)\smapi-internal\Newtonsoft.Json.dll` with `Private=False`.
4. Server:
   - `HttpListener` on `http://localhost:<port>/`, not `127.0.0.1` (see Gotchas);
   - a random token printed in the SMAPI log, required as a header or in the `?token=` query (`<img>` tags can't
     send headers);
   - reject any other `Origin`;
   - JSON with `CamelCaseNamingStrategy { ProcessDictionaryKeys = false }`.
5. The UI is built by Vite into the mod's `wwwroot`. `dotnet build` deploys; `-c Release` also writes the
   release zip.
6. While the game runs the DLL is locked: compile with `-p:EnableModDeploy=false` and deploy once it's closed.
   UI files can be copied while it runs.

## Verification
- **Oracle**:
  - the running game with a lab save;
  - the SMAPI log (`ErrorLogs/SMAPI-latest.txt`), including the mod's own trace lines (treasure chest rolls,
    patch counts);
  - API calls with the token (`curl` or `Invoke-RestMethod`);
  - the human looking at the game in borderless windowed mode, plus screenshots they sent.
- **Validated in game by the human**:
  - phase 1, money edited live;
  - phase 2, player stats, inventory and catalog with icons;
  - phase 3, date, weather, teleport, mines, museum, Community Center and quests;
  - free and instant building, the farm map, and the farmhouse cellar view after a fix.
- **Checked through the API against the running game**:
  - 31 villagers with friendship caps and portraits;
  - world map regions and areas, which revealed the per-tooltip mapping bug;
  - 51 monsters (4 icon misses, fixed with aliases and slime tints);
  - wallet powers (2 non-editable, fixed by adding `PLAYER_HAS_SEEN_EVENT`).
- **Transpiler self-check**: it logs a warning unless it patched exactly two `ldc.r4` (0.4 and 0.6), and none was
  logged.
- **Unit tests**: 26 xUnit tests on the pure rule math (phase scaling, rounding, minimum 1 day, minutes in
  10-minute steps).
- **Not verified in game** (compiled and deployed only):
  - item editor (weapon stats, enchantments over the game's caps, tool level swap, ring fusion);
  - undo and history;
  - machine "every batch" overrides;
  - infinite reach and place anywhere;
  - animal species rules;
  - GMCM page, which wasn't installed;
  - persistence of each edit across a sleep and reload, beyond phase 2;
  - mod-origin attribution in the item catalog, since no content mods were loaded during the tests.

## Gotchas
1. **A copied lab save would overwrite the original** (caught by reading `SaveGame` before the first test).
   **Cause:** the save folder name is `<prefix>_<uniqueIDForThisGame>`, so a copy with the same ID writes into
   the original's folder.
   **Fix:** change `uniqueIDForThisGame` (and `farmName`) in both XML files of the copy.
2. **`HttpListener` on `http://127.0.0.1:<port>/` needs admin rights** (avoided up front). **Cause:** on
   Windows only `localhost` prefixes work without an admin URL ACL. **Fix:** listen on `http://localhost:<port>/` and accept both hosts
   in the Origin check.
3. **The build failed: Newtonsoft.Json not found.** **Cause:** ModBuildConfig 4.4.0 doesn't reference it.
   **Fix:** reference `smapi-internal\Newtonsoft.Json.dll` with `Private=False`.
4. **Monster drop edits had no effect in a French game; the log said "Invalidated 0 cache entries".**
   **Cause:** the game loaded `Data/Monsters.fr-FR`, and invalidating by name misses locale variants.
   **Fix:** `InvalidateCache(a => a.NameWithoutLocale.IsEquivalentTo(...))`.
5. **Dictionary keys came out as "green Slime".** **Cause:** `CamelCasePropertyNamesContractResolver` also
   camelCases dictionary keys. **Fix:** `CamelCaseNamingStrategy { ProcessDictionaryKeys = false }`.
6. **The treasure chest stayed at one item no matter what.** **Cause:** the vanilla loop keeps rolling with
   decaying odds (×0.4), so one item is the common case. Editing the menu on `Display.MenuChanged` also didn't
   stick; that exact cause wasn't pinned down.
   **Fix:** a postfix on `openTreasureMenuEndFunction`, plus a transpiler on its two decay constants for a
   minimum item count. Overflow past 36 slots is dropped as debris.
7. **The world map sent "Beach" to Elliott's cabin and "Town" to Josh's house.** **Cause:** taking the first
   tooltip and first world position of a map area. **Fix:** one clickable spot per tooltip, mapped to the world
   position that overlaps it most (ties go to the name match, then to outdoors). Some areas (the desert) have
   positions that don't overlap at all, so keep zero-overlap candidates ranked last.
8. **Four monsters had no icon, and Green Slime was grey.** **Cause:** slimes share one grey sheet tinted at
   draw time, and Shadow Guy and Skeleton Warrior have no sheet of their own. **Fix:** an alias table plus a
   multiply tint.
9. **Bear's Knowledge and Spring Onion Mastery couldn't be unlocked.** **Cause:** their `Data/Powers`
   condition is `PLAYER_HAS_SEEN_EVENT`, not mail or stats. **Fix:** support that query and set `eventsSeen`.
10. **The farmhouse cellar wasn't reachable in the location viewer.** **Cause:** the cellar stairs aren't in
    `location.warps`; they're a `TouchAction Warp Cellar x y` tile property. **Fix:** also scan Back/TouchAction
    and Buildings/Action tile properties for warp-like actions.
11. **Teleporting to the cellar landed at tile (0,0), outside the map.** **Cause:**
    `Utility.getDefaultWarpLocation` has no entry for it. **Fix:** fall back to where warps into that location
    arrive, then to the nearest tile where `CanSpawnCharacterHere` is true.
12. **The map and building info looked stale.** **Cause:** dynamic PNGs were sent with
    `Cache-Control: max-age=3600`, and side panels only fetched on mount. **Fix:** cache only static icons, and
    reload the panels with the map's refresh counter without resetting a field being typed.
13. **Undo of a weapon edit lost its stats.** **Cause:** `getOne()` doesn't copy them. **Fix:** clone through
    `SaveSerializer`. Clear the history at each save: replaying a snapshot from yesterday would wipe a day of
    play.
14. **"Infinite reach" risk.** **Cause:** `withinRadiusOfPlayer` and `tileWithinRadiusOfPlayer` also drive NPC
    behaviour, such as facing the farmer within a wider radius. **Fix:** only override calls with
    `tileRadius <= 2` for the local player.
15. **A number field rejected the value 1.** **Cause:** in HTML, `min=0.01 step=0.5` makes the valid values
    0.01, 0.51, 1.01… **Fix:** keep `min` a multiple of `step`.
16. **`required` members failed to compile.** **Cause:** `net6.0` lacks `RequiredMemberAttribute`. **Fix:** use
    constructors or init-only properties.
17. **`um publish check --game` reported "game file copied verbatim" for the mod's own files.** **Cause:** the
    deployed copy in `Mods/` is inside the game folder. **Fix:** run the check without `--game` on the extracted
    release zip, or ignore matches under `Mods/<YourMod>`.

## Assets
None generated. Icons, portraits, monster sprites, building textures, the world map and location renders are
read from the player's own install at runtime, through `ParsedItemData.GetTexture()`/`GetSourceRect()`, NPC
portraits, `Characters/Monsters/*`, `Data/WorldMap` textures and xTile sheets. They're encoded to PNG on demand
(un-premultiplying alpha), and none is shipped.

## Cost and time
One long working session (2026-10-05) with the human testing in between. The release zip is about 230 KB:
DLL, web UI, i18n and README.

## Open questions
- Persistence of every edit type across sleep and reload (only phase 2 was checked end to end).
- Behaviour with heavy content mods loaded (PPJA, More Ores…): the catalog's mod attribution and Late-priority
  stacking were designed for it but not exercised.
- The rendered map skips crops, hoe dirt, fruit trees, flooring and the player. Seasonal tile sheets are cached
  per map and season.
- Multiplayer is intentionally unsupported (single-player only).
