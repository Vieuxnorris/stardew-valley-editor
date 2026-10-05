# Valley Editor

A live save and rules editor for **Stardew Valley 1.6**, in your browser. It's a SMAPI mod that runs a small
local website: you change things in the browser and see them happen in the game right away, while it runs.

*En français plus bas.*

## Features

| Tab | What you can do |
|---|---|
| **Player** | Gold, Qi gems, golden walnuts, health, energy, skills and XP, mastery, professions |
| **Inventory** | Drag-and-drop backpack grid, stack and quality; add any item (vanilla or modded) from a searchable catalog with icons; backpack size |
| **Item editor** | Click an item: sell price and edibility; weapon stats (damage, speed, crit…); enchantments, forges and Galaxy Soul at any level; tool level; ring fusion; boots; clothing color; artisan good ingredient |
| **Chests** | Every chest and fridge in the world, edited with the same grid |
| **Relationships** | Friendship and hearts, gifts, talked today, heart events, with portraits |
| **World** | Date, time, weather today and tomorrow, and teleport by clicking the world map |
| **Farm** | The farm drawn as it is now. Click a building to finish or upgrade it (instant, free), change its look, or open the animal door. Go inside to edit chests, machines and animals. Machines can be finished now, given another output, or set to a per-type speed, including instant. Bulk water, ripen, clear dead crops and grow trees |
| **Animals** | Friendship, mood, fullness, name, grow up and produce now. Per-species days between produce and days to grow up, and always deluxe. Pets: friendship, petting, water bowl |
| **Progression** | Collections (shipped, fish, artifacts, minerals, cooking, crafting), special items and powers, mines, Community Center, museum, quests, special orders, mail and event flags |
| **Fishing** | Fish per catch with size and quality, forced fish, per-location fish tables (any season), treasure chest contents and minimum items, fish collection |
| **Monsters** | Drop tables per monster, with icons |
| **Rules** (per save) | Crop growth, fruit and wild trees, machine time, ore and gems per mine level, ladder chance, monster density, sell price, OP mode (instant fishing, loot multiplier, minimum quality, god mode, freeze time, luck, speed, free and instant building, free crafting) |

Every change goes into a **history**, and most of them can be undone (button or Ctrl+Z). The history covers
the current day and starts over at each save.

## Install

1. Install [SMAPI](https://smapi.io) 4.0 or later.
2. Unzip the mod into `Stardew Valley/Mods` (you get `Mods/ValleyEditor`).
3. Start the game through SMAPI and load a save.
4. Type `editor` in the SMAPI console. Your browser opens the editor.

The editor listens on `http://localhost:47800` and only accepts requests carrying the token printed in the
SMAPI console, from its own page. Other websites can't drive your game. You can change the port in
`config.json` or through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098).

Edits stay in memory until the game saves overnight, as in vanilla. A banner reminds you when there are
unsaved changes. Rules are stored per save.

**Single-player only.** Back up your saves before experimenting
(`%APPDATA%\StardewValley\Saves` on Windows).

## Compatibility

- Stardew Valley 1.6.15, SMAPI 4.x, Windows (tested). Linux and macOS should work but are untested.
- Modded items, crops, machines, fish and monsters (Content Patcher, Json Assets…) show up like vanilla ones.
- Rules are applied after other mods' data edits, so multipliers also scale modded content.

## Build from source

Requirements: .NET 6 SDK or later, Node.js 20 or later, and the game installed (ModBuildConfig finds it).

```sh
cd web && npm install && npm run build                # builds the UI into mod/ValleyEditor/wwwroot
cd ../mod && dotnet build ValleyEditor                # builds and deploys to Mods/ValleyEditor
dotnet test ValleyEditor.Tests                        # unit tests for the rule math
```

While the game is running its DLL is locked. Use `dotnet build -p:EnableModDeploy=false` to only compile, or
`npm run dev` in `web/` for a hot-reloading UI proxied to the running game.

## Credits

- Built on [SMAPI](https://smapi.io) and [Harmony](https://github.com/pardeike/Harmony). The UI uses
  [Preact](https://preactjs.com) and [Vite](https://vite.dev).
- Built with AI assistance (Claude Code). The mod ships no game files: icons, maps and portraits are read
  from your own install at runtime.

---

## En français

**Valley Editor** est un éditeur de partie et de règles pour Stardew Valley 1.6, en direct, dans le navigateur.

- **Installation** : installez SMAPI, décompressez le mod dans `Mods`, lancez le jeu, chargez une partie, puis
  tapez `editor` dans la console SMAPI.
- **Fonctions** : toute la liste ci-dessus, avec une interface en français et en anglais.
- **Sauvegarde** : les modifications se sauvegardent comme dans le jeu, pendant la nuit. Les règles sont
  propres à chaque sauvegarde.
- **Historique** : la plupart des actions s'annulent avec Ctrl+Z ou le bouton dédié.
- **Solo uniquement.** Sauvegardez vos parties avant d'expérimenter.
