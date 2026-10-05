# Valley Editor

**A live save and rules editor for Stardew Valley 1.6, in your browser.**

Valley Editor is a [SMAPI](https://smapi.io) mod that runs a small website on your own computer. You change
things in the browser, and they happen in the game right away, while it keeps running. No restart, no save file
hacking.

- Repository and releases: <https://github.com/Vieuxnorris/stardew-valley-editor>
- Version 1.0.0 · Stardew Valley 1.6.15 · SMAPI 4.x · single-player · interface in English and French

*[Version française plus bas.](#en-français)*

---

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Install, update, uninstall](#install-update-uninstall)
- [Using the editor](#using-the-editor)
- [Configuration](#configuration)
- [Security](#security)
- [Compatibility and limitations](#compatibility-and-limitations)
- [Troubleshooting](#troubleshooting)
- [HTTP API](#http-api)
- [Building from source](#building-from-source)
- [Project layout](#project-layout)
- [Contributing](#contributing)
- [Credits and license](#credits-and-license)
- [En français](#en-français)

---

## Features

### Player
- Gold, Qi gems, golden walnuts, current and max health and energy.
- The five skills (level or exact XP), mastery XP, professions.
- New levels queue the usual level-up screens, which show when you sleep.

### Inventory and items
- Backpack grid with drag and drop, stack and quality per slot, and backpack size (12 to 48 slots).
- A searchable **catalog of every item**, vanilla and modded, with icons, type filters and the mod each item
  comes from.
- **Item editor**: click any item in the backpack or a chest. The panel adapts to its type:

| Item type | What you can change |
|---|---|
| Any object | sell price, edibility (energy), the ingredient of an artisan good (wine of *X*, jelly of *Y*…) |
| Weapons | min/max damage, speed, precision, defense, area of effect, knockback, crit chance and multiplier (vanilla values shown) |
| Enchantments | forges (ruby, emerald, aquamarine, jade, amethyst, topaz), Galaxy Soul and innate enchantments at **any level up to 99**; prismatic enchantments as checkboxes, **several at once** |
| Tools | upgrade level (copper → iridium, every fishing rod), keeping enchantments and bait/tackle; refill the watering can |
| Rings | fuse with any other ring |
| Boots | defense and immunity |
| Clothing | color |

### Chests
- Every chest and fridge in every location, with its color and fill level.
- Each one opens in the same grid as the backpack.

### Relationships
- Every villager with their portrait: friendship points and hearts (capped like the game), dating and marriage
  status, gifts this week and today, talked today.
- Bulk actions.

### World
- Date (day, season, year), time, and weather today and tomorrow for each location context (valley, island…).
- **Teleport by clicking the world map** (valley and Ginger Island; every building is clickable), or through a
  list of every location.

### Farm
- **Crops and trees** per location (farm, greenhouse, island…): counts of crops, crops needing water, ready
  crops, dead crops and young trees. Bulk actions to water, ripen, clear dead crops, grow trees, fill fruit trees
  and finish machines.
- **The farm drawn as it is now**: ground, objects, trees, buildings, furniture and animals. Click on the map:
  - **a building**: finish its construction, upgrade it **instantly and for free**, change its skin, open or close the animal door, and **go inside**;
  - **an exit** (doors, stairs, map edges): follow it, for example house → cellar. The breadcrumb takes you back;
  - **a chest or the fridge**: edit its contents;
  - **an animal**: edit it (see below);
  - **a machine**: see its input, output and time left, then:
    - **finish it now**;
    - change its output with icons or the catalog, plus quantity and quality, for **this batch only**, **every new batch of this machine** or **every machine of this type**;
    - set the **speed of its type**, from ×2 slower to **instant**.
- Refresh button, auto-refresh every 5 s, last update time.
- **Farmhouse**: free upgrade, ready the next morning.

### Animals
- Every farm animal: name, friendship (hearts), mood, fullness, age, today's produce and mood text.
- Grow up and **produce now**: eggs go to your backpack in iridium quality, while milk and wool become ready to
  collect.
- **Species rules**: days between produce, days to grow up, always the deluxe produce.
- **Pets**: name, friendship, pet today, fill the water bowl.
- One button pampers everyone.

### Progression
- **Collections**: shipped items, fish, artifacts, minerals, cooking, crafting. Each shows progress, with
  "Complete" and "Complete everything".
- **Special items and powers** (the wallet page, read from `Data/Powers`, so modded entries are included): get
  or remove each one, or unlock everything. Bear's Knowledge and Spring Onion Mastery are included.
- Mines (elevator level, Skull Cavern depth), Community Center rooms, museum "donate everything", quests and
  special orders, mail and event flags.

### Fishing
- Fish per catch with forced size and quality, and a forced fish.
- **Fish tables per location**: chances, removed fish, added fish, **any season**, "no restrictions",
  "priority".
- **Treasure chest**: its own loot table, a multiplier, and a **minimum number of items**.
- Fish collection.

### Monsters
- An icon grid of every monster, each with an editable **drop table**.

### Rules (saved per save file)
- Crop growth (global and per crop, also for already planted crops), fruit trees, wild trees, machine
  processing time (**0 = instant**).
- Mines: ore per level band, stones, monsters, gems, a guaranteed ladder.
- Sell price multiplier, pickup multiplier, minimum quality, monster loot rolls.
- **OP mode**:
  - instant fishing, perfect catch, a treasure every time;
  - infinite health and energy, frozen time, max luck;
  - speed, magnet and luck bonuses;
  - **free building**, **instant building** and **free crafting**;
  - **infinite reach** and **place objects anywhere**.

### History and undo
- Every change made through the editor is listed in **History** (🕘).
- **Undo the last change** with the button or **Ctrl+Z**. This covers player, inventory and chests (including
  the item editor), date and time, every rule, fishing, monsters and machines. Other changes are marked
  "final".
- The history starts over at each save (overnight), so an undo never wipes a day of play.

---

## Requirements

- Stardew Valley **1.6** (tested on 1.6.15, Steam, Windows).
- [SMAPI](https://smapi.io) **4.0 or later** (tested on 4.5.2).
- A web browser on the same computer.
- Optional: [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) to change settings in
  game.

## Install, update, uninstall

**Install**
1. Install SMAPI.
2. Download `ValleyEditor 1.0.0.zip` from the
   [releases](https://github.com/Vieuxnorris/stardew-valley-editor/releases).
3. Unzip it into `Stardew Valley/Mods`. You should get `Mods/ValleyEditor/manifest.json`.
4. Start the game through SMAPI.

**Update**: delete `Mods/ValleyEditor` and unzip the new version. Keep `config.json` if you changed it.
Per-save rules are stored in your saves, not in the mod folder.

**Uninstall**: delete `Mods/ValleyEditor`. Your saves keep the edits you made, since they're ordinary game
data. Per-save rules stop applying, because they're read by the mod.

## Using the editor

1. Load a save.
2. Type **`editor`** in the SMAPI console window. Your browser opens the editor.
   - The address is also printed in the SMAPI log: `http://localhost:47800/?token=…`.
   - To open it automatically, turn on "Open the editor when a save loads" (see Configuration).
3. Pick a tab, change something, click **Apply**. The game updates within a frame.

Good to know:
- **Edits are saved like everything else in Stardew: when you sleep.** A yellow banner reminds you when there
  are unsaved changes. Quitting without sleeping loses them, as in vanilla.
- **Rules are per save**: they're stored in the save file and come back when you load it.
- Play in **borderless windowed** mode to watch the game while using the browser. If the game freezes whenever
  the browser has focus, turn off "Pause when the game window is inactive" in the game options: the editor can
  only act while the game updates.
- Language: FR/EN switch at the top right. The choice is remembered.

## Configuration

`Mods/ValleyEditor/config.json` is created on first launch:

| Setting | Default | Meaning |
|---|---|---|
| `Port` | `47800` | The localhost port the editor listens on. Change it if another program uses it. |
| `OpenBrowserOnSaveLoaded` | `false` | Open the editor in the browser every time a save loads. |

With Generic Mod Config Menu installed, both settings are in its menu, in English and French. A new port takes
effect when you save, with no restart.

## Security

- The editor listens on **`localhost` only**, so other computers on your network can't reach it.
- Every API call needs the **random token** generated at each game launch and printed in the SMAPI console.
- Calls coming from **other websites are refused** (Origin check), so a page you visit can't drive your game
  even if it guesses the port.
- Nothing is sent anywhere: no telemetry, no external requests.

## Compatibility and limitations

- **Single-player only.** Multiplayer isn't supported. Don't use it on someone else's server.
- Modded content (Content Patcher, Json Assets…) shows up like vanilla: items, crops, machines, fish, monsters,
  powers, buildings. Rules are applied after other mods' edits (`Late` priority), so multipliers also scale
  modded content.
- Designed to sit alongside cheat menus such as CJB Cheats Menu (not tested together). If two mods change the same thing, the last one to act wins.
- The farm map doesn't draw crops, tilled soil, flooring, fruit trees or the player yet. Machines, chests and
  animals on it are clickable.
- Weapon stats edited here are saved as they are. Forging or unforging the weapon at Clint's recalculates them
  from the game data.
- Farmhouse upgrades complete overnight. The game rebuilds the house then, and it isn't safe to do it under your
  feet.
- **Back up your saves** before big experiments (`%APPDATA%\StardewValley\Saves` on Windows).

## Troubleshooting

| Problem | Fix |
|---|---|
| "Can't reach the game" banner | Check that the game runs through SMAPI and that the SMAPI log says `Editor running at …`. |
| "Missing token" | Open the editor with the `editor` command, or copy the full URL with `?token=` from the SMAPI log. The token changes at each launch. |
| `Couldn't start the editor on port 47800` in the log | Another program uses the port: change `Port` in `config.json` (or GMCM). |
| Edits disappeared after quitting | Edits are saved when you sleep, as in vanilla. |
| "The game did not respond in time" | The game is paused (menu, cutscene, unfocused window with pause on). Click into the game and retry. |
| Map or panel looks outdated | Click ↻ or turn on auto-refresh. Reload the page (F5) after updating the mod. |

When reporting a bug, attach your SMAPI log: <https://smapi.io/log>.

---

## HTTP API

The web UI is a plain client of a JSON API, which you can script too.

- **Base URL**: `http://localhost:<Port>/api/`.
- **Authentication**: header `X-Editor-Token: <token>`, or `?token=<token>` in the query string (for images).
- **Format**: requests and responses are JSON with camelCase fields. Errors return `{ "error": "…" }` with
  status 400, 401, 403, 404, 409 or 503.
- **Write calls** (`POST`, `PUT`, `PATCH`, `DELETE`) run on the game thread, get recorded in the history, and
  usually return the updated state.

```sh
TOKEN=...   # from the SMAPI log
curl -H "X-Editor-Token: $TOKEN" http://localhost:47800/api/player
curl -H "X-Editor-Token: $TOKEN" -H "Content-Type: application/json" \
     -X PATCH -d '{"money": 1000000}' http://localhost:47800/api/player
```

| Area | Routes |
|---|---|
| Status | `GET /status` |
| Player | `GET/PATCH /player` · `PUT /player/skills/{skill}` · `PUT /player/professions` |
| Inventory | `GET/POST /inventory` · `PATCH/DELETE /inventory/{slot}` · `POST /inventory/swap` · `PUT /inventory/size` · `GET/PATCH /inventory/{slot}/details` |
| Chests | `GET /chests` · same slot routes under `/chests/{chest}` (id `Location@x,y` or `Location@fridge`) |
| Items | `GET /items?q=&type=&mod=&offset=&limit=` · `GET /items/facets` · `GET /sprites/{qualifiedId}` |
| Villagers | `GET /npcs` · `PATCH /npcs/{name}` · `POST /npcs/bulk` · `GET /portraits/{name}` |
| World | `GET/PATCH /world` · `PUT /world/weather/{context}` · `POST /world/warp` · `GET /world/map` · `GET /world/map/{region}/image` |
| Farm | `GET /farm` · `POST /farm/crops` · `POST /farm/house` · `GET /farm/view/{location}` · `GET /farm/map/{location}/image` · `POST /farm/buildings/{id}/finish` · `POST /farm/buildings/{id}/upgrade` · `PUT /farm/buildings/{id}/skin` · `POST /farm/buildings/{id}/animal-door` · `GET /building-sprites/{id}` |
| Machines | `GET /machines/{id}` · `POST /machines/{id}/finish` · `POST /machines/finish-all` · `PUT /machines/{id}/output` · `PUT /machines/{id}/output-rule` · `PUT /machines/types/{machine}/speed` |
| Animals | `GET /animals` · `PATCH /animals/{id}` · `POST /animals/{id}/grow` · `POST /animals/{id}/produce` · `POST /animals/pamper` · `GET/PUT /animals/types/{type}` · `GET /animal-sprites/{id}` |
| Pets | `GET /pets` · `PATCH /pets/{id}` · `POST /pets/{id}/pet` · `POST /pets/{id}/water` · `GET /pet-sprites/{id}` |
| Progression | `GET /progression` · `PUT /progression/unlocks/{id}` · `PATCH /progression/mines` · `POST /progression/community-center/{area}` · `POST /progression/museum/donate-all` · `GET/PUT /progression/flags/{mail\|events}` |
| Collections | `GET /collections` · `POST /collections/{category}/complete` · `PUT /collections/powers/{id}` · `GET /power-sprites/{id}` |
| Quests | `GET/POST /quests` · `GET /quests/catalog` · `POST /quests/{index}/complete` · `DELETE /quests/{index}` · `GET /quests/special-orders` · `POST /quests/special-orders/{index}/complete` · `PUT /quests/special-orders/completed` |
| Fishing | `GET/PATCH /fishing` · `GET /fishing/tables` · `PUT /fishing/tables/{location}` · `PUT /fishing/treasure` · `PUT /fishing/collection/{fishId}` · `POST /fishing/collection/catch-all` |
| Monsters | `GET /monsters` · `PUT/DELETE /monsters/{name}/drops` · `GET /monster-sprites/{name}` |
| Rules | `GET/PATCH /rules` · `POST /rules/reset` · `POST /rules/apply-to-planted-crops` |
| History | `GET /history` · `POST /history/undo` |

The exact request bodies are in the domain classes under `mod/ValleyEditor/Domains/`; each route validates its
fields and explains what's wrong in the error message.

---

## Building from source

**Requirements**
- .NET SDK 6 or later (8 tested).
- Node.js 20 or later (24 tested).
- The game installed. [ModBuildConfig](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig) finds
  it; set `GamePath` if it doesn't.

```sh
git clone https://github.com/Vieuxnorris/stardew-valley-editor.git
cd stardew-valley-editor

cd web
npm install
npm run build              # type-checks, then builds the UI into mod/ValleyEditor/wwwroot

cd ../mod
dotnet build ValleyEditor  # builds and deploys to <game>/Mods/ValleyEditor
dotnet test ValleyEditor.Tests
```

- **Release zip**: `dotnet build ValleyEditor -c Release -p:ModZipPath=<folder>` writes
  `ValleyEditor <version>.zip`.
- **While the game runs**, its DLL is locked. Build with `-p:EnableModDeploy=false` to only compile, then deploy
  once the game is closed. UI files can be copied over while it runs; reload the page.
- **UI hot reload**: with the game running and a save loaded, run `npm run dev` in `web/` and open the printed
  URL with `?token=<token>`. API calls are proxied to the game.

## Project layout

```
mod/ValleyEditor/            the SMAPI mod (C#, net6.0)
  ModEntry.cs                entry point: config, server, domains, events, console command
  Server/                    HttpListener server, router, game-thread dispatcher
  Domains/                   one class per API area (Player, Inventory, Farm, Machines, Animals…)
  Rules/                     per-save rules, asset edits and Harmony patches (mines, fishing, cheats, machines)
  Sprites/                   PNG encoding, item/monster/portrait icons, location map renderer
  Integrations/              Generic Mod Config Menu
  i18n/                      mod translations (config menu)
  wwwroot/                   built web UI (generated, not committed)
mod/ValleyEditor.Tests/      xUnit tests for the rule math
web/                         the web UI (Vite, TypeScript, Preact)
  src/tabs/                  one component per tab or panel
  src/i18n.tsx               French and English texts
docs/ROADMAP.md              roadmap and status
docs/FIELD_NOTE.md           technical write-up: engine facts, verification, gotchas
MODLOG.md                    development journal (French)
```

## Contributing

Issues and pull requests are welcome on [GitHub](https://github.com/Vieuxnorris/stardew-valley-editor).

- Include your SMAPI log (<https://smapi.io/log>) with bug reports.
- Keep game files, decompiled code and saves out of the repository.
- Run `npm run build` (it type-checks) and `dotnet test` before opening a PR.
- New UI texts need both a French and an English entry in `web/src/i18n.tsx`. TypeScript fails the build if
  the English entry is missing.

## Credits and license

- By [Vieuxnorris](https://github.com/Vieuxnorris).
- Built with AI assistance (Claude Code). The technical write-up is in `docs/FIELD_NOTE.md`.
- Built on [SMAPI](https://smapi.io) and [Harmony](https://github.com/pardeike/Harmony). The UI uses
  [Preact](https://preactjs.com) and [Vite](https://vite.dev).
- The mod ships **no game files**: icons, portraits, maps and sprites are read from your own installation at
  runtime.
- Stardew Valley is © ConcernedApe. This project isn't affiliated with ConcernedApe.
- License: [MIT](LICENSE).

---

## En français

**Valley Editor** est un éditeur de partie et de règles pour **Stardew Valley 1.6**, en direct, dans le
navigateur. C'est un mod [SMAPI](https://smapi.io) qui fait tourner un petit site sur votre ordinateur : ce que
vous changez dans la page s'applique tout de suite dans le jeu.

### Installation
1. Installez SMAPI 4.0 ou plus.
2. Téléchargez `ValleyEditor 1.0.0.zip` depuis les
   [releases](https://github.com/Vieuxnorris/stardew-valley-editor/releases) et décompressez-le dans
   `Stardew Valley/Mods`.
3. Lancez le jeu via SMAPI et chargez une partie.
4. Tapez **`editor`** dans la console SMAPI : l'éditeur s'ouvre dans le navigateur.

### Ce qu'on peut faire

| Onglet | Fonctions |
|---|---|
| **Joueur** | or, gemmes Qi, noix dorées, PV, énergie, compétences, maîtrise, professions |
| **Inventaire** | grille du sac, catalogue de tous les objets (moddés compris) avec icônes |
| **Éditeur d'objets** | dégâts, vitesse et critiques des armes ; enchantements et forges jusqu'au niveau 99 ; niveau des outils ; fusion d'anneaux ; bottes ; couleur des vêtements |
| **Coffres** | tous les coffres et frigos du monde |
| **Relations** | amitié, cœurs et cadeaux des villageois |
| **Monde** | date, heure, météo, téléportation en cliquant sur la carte du monde |
| **Ferme** | la ferme dessinée telle qu'elle est ; bâtiments à terminer, améliorer, ré-habiller ou visiter ; sorties à suivre (maison → cave) ; machines (terminer, production de chaque lot, vitesse jusqu'à instantanée) ; actions en masse sur les cultures |
| **Animaux** | amitié, humeur, « produire maintenant », règles par espèce, animal de compagnie |
| **Progression** | collections, objets spéciaux et pouvoirs, mines, Centre communautaire, musée, quêtes |
| **Pêche** | tables de pêche par lieu (toutes saisons), coffre au trésor, collection |
| **Monstres** | table de butin de chaque monstre |
| **Règles** | vitesse des cultures et des machines, minerais de la mine, prix, et le mode OP (pêche instantanée, god mode, construction gratuite et instantanée, artisanat gratuit, portée infinie, pose n'importe où…) |

L'**historique** (🕘) liste chaque modification, et **Ctrl+Z** annule la dernière.

### À savoir
- Les modifications sont sauvegardées **quand vous dormez**, comme dans le jeu. Un bandeau le rappelle.
- Les **règles sont propres à chaque sauvegarde**.
- **Solo uniquement.** Sauvegardez vos parties avant de grosses expériences.
- Configuration (port, ouverture automatique) : `config.json`, ou Generic Mod Config Menu.
- Sécurité : l'éditeur n'écoute que sur `localhost`, exige un jeton généré à chaque lancement, et refuse les
  appels venant d'autres sites.

Licence MIT. Le mod ne contient aucun fichier du jeu.
