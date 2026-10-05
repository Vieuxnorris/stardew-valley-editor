using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using ValleyEditor.Server;
using ValleyEditor.Sprites;

namespace ValleyEditor.Domains;

/// <summary>Crops, trees, buildings and the farmhouse, in every location.</summary>
internal sealed class FarmDomain : Domain
{
    /// <summary>The farmhouse's last vanilla upgrade (kitchen, then nursery, then cellar).</summary>
    private const int MaxHouseLevel = 3;

    private static readonly string[] CropActions = { "water", "ripen", "clearDead", "trees" };

    private readonly ItemSprites sprites;

    public FarmDomain(GameThreadDispatcher game, EditorState state, ItemSprites sprites)
        : base(game, state)
    {
        this.sprites = sprites;
    }

    public override void Register(Router router)
    {
        router.Get("/api/farm", _ => this.Read(Snapshot));

        // a bulk action on the crops and trees of one location, or of every location when 'location' is omitted
        router.Post("/api/farm/crops", request =>
        {
            string action = request.BodyObject.Value<string>("action") ?? "";
            if (!CropActions.Contains(action))
                throw new ApiException(400, $"'action' must be one of: {string.Join(", ", CropActions)}.");
            string? location = request.BodyObject.Value<string>("location");

            return this.Write(() =>
            {
                List<GameLocation> targets = location is null
                    ? Locations().ToList()
                    : new List<GameLocation> { FindLocation(location) };
                int changed = targets.Sum(l => ApplyCropAction(l, action));
                return new { Changed = changed, Farm = Snapshot() };
            });
        });

        router.Post("/api/farm/buildings/{building}/finish", request => this.Write(() =>
        {
            Building building = FindBuilding(request.Params["building"]);
            if (building.daysOfConstructionLeft.Value <= 0 && building.daysUntilUpgrade.Value <= 0)
                throw new ApiException(409, "This building isn't under construction.");
            building.FinishConstruction();
            return Snapshot();
        }));

        router.Post("/api/farm/buildings/{building}/upgrade", request =>
        {
            string target = request.BodyObject.Value<string>("type") ?? throw new ApiException(400, "'type' is required.");
            return this.Write(() =>
            {
                Building building = FindBuilding(request.Params["building"]);
                if (building.isUnderConstruction(ignoreUpgrades: false))
                    throw new ApiException(409, "Finish the current construction first.");
                if (!Upgrades(building.buildingType.Value).Any(u => u.Type == target))
                    throw new ApiException(400, $"'{building.buildingType.Value}' can't be upgraded to '{target}'.");

                // the game's own upgrade path, finished at once
                building.upgradeName.Value = target;
                building.daysUntilUpgrade.Value = 1;
                building.FinishConstruction();
                return Snapshot();
            });
        });

        router.Get("/api/building-sprites/{building}", async request =>
            new BinaryResult(await this.sprites.RenderPng(() => ItemSprites.BuildingPixels(FindBuilding(request.Params["building"]))), "image/png"));

        // what's clickable in a location (its buildings, chests and animals), and the building it's inside of, if any
        router.Get("/api/farm/view/{location}", request => this.Read(() => View(FindLocation(request.Params["location"]))));

        // a location drawn like in game, for the clickable buildings map
        router.Get("/api/farm/map/{location}/image", async request =>
            new BinaryResult(await this.sprites.RenderPng(() => MapRenderer.Render(FindLocation(request.Params["location"]))), "image/png"));

        router.Put("/api/farm/buildings/{building}/skin", request =>
        {
            string? skin = request.BodyObject.Value<string>("skin");
            return this.Write(() =>
            {
                Building building = FindBuilding(request.Params["building"]);
                BuildingData data = building.GetData() ?? throw new ApiException(409, "This building has no data.");
                if (skin != null && !data.Skins.Any(s => s.Id == skin))
                    throw new ApiException(400, $"'{building.buildingType.Value}' has no skin '{skin}'.");

                // like the game's skin menu: new skin, default paint
                building.skinId.Value = skin;
                building.netBuildingPaintColor.Value.Color1Default.Value = true;
                building.netBuildingPaintColor.Value.Color2Default.Value = true;
                building.netBuildingPaintColor.Value.Color3Default.Value = true;
                building.resetTexture();
                return Snapshot();
            });
        });

        router.Post("/api/farm/buildings/{building}/animal-door", request =>
        {
            bool open = request.BodyObject.Value<bool?>("open") ?? throw new ApiException(400, "'open' must be true or false.");
            return this.Write(() =>
            {
                Building building = FindBuilding(request.Params["building"]);
                if (building.GetIndoors() is not AnimalHouse)
                    throw new ApiException(409, "This building has no animal door.");
                building.animalDoorOpen.Value = open;
                return Snapshot();
            });
        });

        // the farmhouse is rebuilt overnight by the game (furniture moved, new map), so upgrades are scheduled for tomorrow
        router.Post("/api/farm/house", request =>
        {
            string action = request.BodyObject.Value<string>("action") ?? "";
            return this.Write(() =>
            {
                Farmer player = Game1.player;
                switch (action)
                {
                    case "upgrade":
                        if (player.HouseUpgradeLevel >= MaxHouseLevel)
                            throw new ApiException(409, "The farmhouse is already fully upgraded.");
                        player.daysUntilHouseUpgrade.Value = 1;
                        break;
                    case "cancel":
                        player.daysUntilHouseUpgrade.Value = -1;
                        break;
                    default:
                        throw new ApiException(400, "'action' must be 'upgrade' or 'cancel'.");
                }
                return Snapshot();
            });
        });
    }

    private static object Snapshot()
    {
        Farmer player = Game1.player;
        return new
        {
            Fields = Locations()
                .Select(FieldStats)
                .Where(f => f.Crops + f.FruitTrees + f.YoungTrees > 0)
                .OrderBy(f => f.Location == "Farm" ? 0 : 1)
                .ThenBy(f => f.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            // the locations with buildings, for the map
            BuildingLocations = Locations()
                .Where(l => l.buildings.Count > 0)
                .Select(l =>
                {
                    Point size = MapRenderer.Size(l);
                    return new { Location = l.NameOrUniqueName, DisplayName = l.DisplayName ?? l.Name, Width = size.X, Height = size.Y };
                })
                .ToArray(),
            Buildings = AllBuildings()
                .Select(b =>
                {
                    BuildingData? data = b.Building.GetData();
                    Rectangle sprite = MapRenderer.BuildingBounds(b.Building);
                    const int tile = MapRenderer.TileSize;
                    return new
                    {
                        b.Id,
                        Location = b.Location.NameOrUniqueName,
                        Sprite = new { sprite.X, sprite.Y, sprite.Width, sprite.Height },
                        Footprint = new { X = b.Building.tileX.Value * tile, Y = b.Building.tileY.Value * tile, Width = b.Building.tilesWide.Value * tile, Height = b.Building.tilesHigh.Value * tile },
                        SkinId = b.Building.skinId.Value,
                        Skins = data?.Skins.Select(s => new { s.Id, Name = s.Name is { Length: > 0 } skinName ? TokenParser.ParseText(skinName) ?? s.Id : s.Id }).ToArray() ?? Array.Empty<object>(),
                        Interior = b.Building.GetIndoors()?.NameOrUniqueName,
                        HasAnimalDoor = b.Building.GetIndoors() is AnimalHouse,
                        AnimalDoorOpen = b.Building.animalDoorOpen.Value,
                        Type = b.Building.buildingType.Value,
                        Name = BuildingName(b.Building.buildingType.Value, data),
                        LocationName = b.Location.DisplayName ?? b.Location.Name,
                        X = b.Building.tileX.Value,
                        Y = b.Building.tileY.Value,
                        DaysOfConstructionLeft = b.Building.daysOfConstructionLeft.Value,
                        DaysUntilUpgrade = b.Building.daysUntilUpgrade.Value,
                        UpgradeName = b.Building.daysUntilUpgrade.Value > 0 ? b.Building.upgradeName.Value : null,
                        Animals = b.Building.GetIndoors() is AnimalHouse house ? house.animalsThatLiveHere.Count : (int?)null,
                        AnimalLimit = b.Building.GetIndoors() is AnimalHouse house2 ? house2.animalLimit.Value : (int?)null,
                        Upgrades = Upgrades(b.Building.buildingType.Value).ToArray(),
                    };
                })
                .ToArray(),
            House = new
            {
                Level = player.HouseUpgradeLevel,
                MaxLevel = MaxHouseLevel,
                DaysUntilUpgrade = player.daysUntilHouseUpgrade.Value,
            },
        };
    }

    private static object View(GameLocation location)
    {
        Point size = MapRenderer.Size(location);
        string name = location.NameOrUniqueName;
        var parent = AllBuildings().FirstOrDefault(b => b.Building.GetIndoors()?.NameOrUniqueName == name);
        const int tile = MapRenderer.TileSize;

        var chests = new List<object>();
        foreach ((Vector2 pos, StardewValley.Object obj) in location.objects.Pairs)
        {
            if (obj is Chest { playerChest.Value: true } chest)
                chests.Add(new { Id = $"{name}@{(int)pos.X},{(int)pos.Y}", Name = chest.DisplayName, X = (int)pos.X * tile, Y = (int)pos.Y * tile - tile, Width = tile, Height = tile * 2 });
        }
        Point? fridge = location switch
        {
            FarmHouse house when house.fridgePosition != Point.Zero => house.fridgePosition,
            IslandFarmHouse island when island.fridgePosition != Point.Zero => island.fridgePosition,
            _ => null,
        };
        if (fridge is { } f)
            chests.Add(new { Id = $"{name}@fridge", Name = (string?)null, X = f.X * tile, Y = f.Y * tile - tile, Width = tile, Height = tile * 2 });

        return new
        {
            Location = name,
            DisplayName = location.DisplayName ?? location.Name,
            Width = size.X,
            Height = size.Y,
            Parent = parent.Building is null ? null : new { Location = parent.Location.NameOrUniqueName, DisplayName = parent.Location.DisplayName ?? parent.Location.Name, BuildingId = parent.Id },
            Chests = chests,
            Animals = location.animals.Values
                .Select(a =>
                {
                    Rectangle r = MapRenderer.CharacterBounds(a);
                    return new { Id = a.myID.Value.ToString(), a.Name, r.X, r.Y, r.Width, r.Height };
                })
                .ToArray(),
        };
    }

    private record Field(string Location, string DisplayName, int Crops, int Dry, int Ready, int Dead, int FruitTrees, int YoungTrees);

    private static Field FieldStats(GameLocation location)
    {
        int crops = 0, dry = 0, ready = 0, dead = 0, fruitTrees = 0, youngTrees = 0;
        foreach (HoeDirt dirt in Dirt(location))
        {
            if (dirt.crop is not { } crop)
                continue;
            crops++;
            if (crop.dead.Value)
                dead++;
            else if (IsReady(crop))
                ready++;
            else if (dirt.needsWatering() && !dirt.isWatered())
                dry++;
        }
        foreach (TerrainFeature feature in location.terrainFeatures.Values)
        {
            if (feature is FruitTree fruitTree)
            {
                fruitTrees++;
                if (fruitTree.growthStage.Value < FruitTree.treeStage)
                    youngTrees++;
            }
            else if (feature is Tree tree && !tree.stump.Value && tree.growthStage.Value < Tree.treeStage)
                youngTrees++;
        }
        return new Field(location.NameOrUniqueName, location.DisplayName ?? location.Name, crops, dry, ready, dead, fruitTrees, youngTrees);
    }

    /// <summary>The game's harvest check (Crop.harvest): last phase, and regrowing crops back to day 0.</summary>
    private static bool IsReady(Crop crop) => crop.currentPhase.Value >= crop.phaseDays.Count - 1 && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0);

    private static int ApplyCropAction(GameLocation location, string action)
    {
        int changed = 0;
        switch (action)
        {
            case "water":
                foreach (HoeDirt dirt in Dirt(location))
                {
                    if (dirt.state.Value != HoeDirt.watered && dirt.state.Value != HoeDirt.invisible)
                    {
                        dirt.state.Value = HoeDirt.watered;
                        changed++;
                    }
                }
                break;

            case "ripen":
                foreach (HoeDirt dirt in Dirt(location))
                {
                    if (dirt.crop is { } crop && !crop.dead.Value && !IsReady(crop))
                    {
                        crop.growCompletely();
                        changed++;
                    }
                }
                break;

            case "clearDead":
                foreach (HoeDirt dirt in Dirt(location))
                {
                    if (dirt.crop is { dead.Value: true })
                    {
                        dirt.crop = null;
                        changed++;
                    }
                }
                break;

            case "trees":
                foreach (TerrainFeature feature in location.terrainFeatures.Values)
                {
                    if (feature is FruitTree fruitTree)
                    {
                        if (fruitTree.growthStage.Value < FruitTree.treeStage)
                        {
                            fruitTree.daysUntilMature.Value = 0;
                            fruitTree.growthStage.Value = FruitTree.treeStage;
                            changed++;
                        }
                        // fill the tree with fruit, if it's in season here
                        while (fruitTree.fruit.Count < FruitTree.maxFruitsOnTrees && fruitTree.TryAddFruit())
                            changed++;
                    }
                    else if (feature is Tree tree && !tree.stump.Value && tree.growthStage.Value < Tree.treeStage)
                    {
                        tree.growthStage.Value = Tree.treeStage;
                        changed++;
                    }
                }
                break;
        }
        return changed;
    }

    /// <summary>Tilled soil, including garden pots.</summary>
    private static IEnumerable<HoeDirt> Dirt(GameLocation location)
    {
        foreach (TerrainFeature feature in location.terrainFeatures.Values)
        {
            if (feature is HoeDirt dirt)
                yield return dirt;
        }
        foreach (StardewValley.Object obj in location.objects.Values)
        {
            if (obj is IndoorPot { hoeDirt.Value: { } potDirt })
                yield return potDirt;
        }
    }

    private static IEnumerable<GameLocation> Locations()
    {
        var all = new List<GameLocation>();
        Utility.ForEachLocation(l =>
        {
            all.Add(l);
            return true;
        });
        return all;
    }

    private static GameLocation FindLocation(string name)
    {
        return Locations().FirstOrDefault(l => l.NameOrUniqueName == name)
            ?? throw new ApiException(404, $"Unknown location '{name}'.");
    }

    /// <summary>Every building, with an ID of the form <c>location@x,y</c>.</summary>
    private static IEnumerable<(string Id, GameLocation Location, Building Building)> AllBuildings()
    {
        return Locations()
            .SelectMany(l => l.buildings.Select(b => ($"{l.NameOrUniqueName}@{b.tileX.Value},{b.tileY.Value}", l, b)))
            .ToList();
    }

    private static Building FindBuilding(string id)
    {
        return AllBuildings().FirstOrDefault(b => b.Id == id).Building
            ?? throw new ApiException(404, $"No building '{id}'. It may have been moved; reload the list.");
    }

    /// <summary>The buildings that list this one as their <c>BuildingToUpgrade</c> (Coop → Big Coop, etc.).</summary>
    private static IEnumerable<BuildingUpgrade> Upgrades(string type)
    {
        return DataLoader.Buildings(Game1.content)
            .Where(pair => pair.Value.BuildingToUpgrade == type)
            .Select(pair => new BuildingUpgrade(pair.Key, BuildingName(pair.Key, pair.Value)));
    }

    private record BuildingUpgrade(string Type, string Name);

    private static string BuildingName(string type, BuildingData? data)
    {
        return data?.Name is { Length: > 0 } name ? TokenParser.ParseText(name) ?? type : type;
    }
}
