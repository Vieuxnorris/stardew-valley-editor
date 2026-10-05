using System.Collections.Generic;

namespace ValleyEditor.Rules;

/// <summary>An item that may be added to some loot: a treasure chest, a monster's drops, a fishing spot.</summary>
internal sealed class LootEntry
{
    /// <summary>Qualified item ID, like (O)74.</summary>
    public string ItemId { get; set; } = "";

    /// <summary>Chance to get it, from 0 to 1.</summary>
    public double Chance { get; set; } = 1;

    public int MinStack { get; set; } = 1;

    public int MaxStack { get; set; } = 1;

    /// <summary>Item quality (0, 1, 2, 4).</summary>
    public int Quality { get; set; }
}

/// <summary>Changes to one location's fishing spawn table (Data/Locations → Fish).</summary>
internal sealed class FishTableEdit
{
    /// <summary>New chance for existing entries, by entry ID.</summary>
    public Dictionary<string, float> Chances { get; set; } = new();

    /// <summary>Entry IDs removed from the table.</summary>
    public HashSet<string> Removed { get; set; } = new();

    /// <summary>Fish added to the table; they're rolled before vanilla entries and ignore season/time requirements.</summary>
    public List<LootEntry> Added { get; set; } = new();

    /// <summary>Season overrides by entry ID: a season name, or "any" to allow every season.</summary>
    public Dictionary<string, string> Seasons { get; set; } = new();

    /// <summary>Entry IDs freed from their condition, Data/Fish time and weather requirements, and catch limit.</summary>
    public HashSet<string> Unrestricted { get; set; } = new();

    /// <summary>Entry IDs rolled before the other entries, so their chance is what they actually get.</summary>
    public HashSet<string> Priority { get; set; } = new();

    public bool IsEmpty => this.Chances.Count == 0 && this.Removed.Count == 0 && this.Added.Count == 0
        && this.Seasons.Count == 0 && this.Unrestricted.Count == 0 && this.Priority.Count == 0;
}

/// <summary>Game rule overrides for one save, stored in the save file through SMAPI. All defaults are vanilla.</summary>
internal sealed class RulesData
{
    /// <summary>Crop growth time (and regrowth) multiplier; 0.5 means twice as fast.</summary>
    public double CropGrowth { get; set; } = 1;

    /// <summary>Per-crop growth multipliers by seed ID, replacing <see cref="CropGrowth"/> for that crop.</summary>
    public Dictionary<string, double> CropGrowthOverrides { get; set; } = new();

    /// <summary>Days of growth a young fruit tree gains per day (vanilla 1).</summary>
    public int FruitTreeSpeed { get; set; } = 1;

    /// <summary>Multiplier on the daily chance for wild trees (oak, maple...) to grow a stage.</summary>
    public double WildTreeGrowth { get; set; } = 1;

    /// <summary>Machine processing time multiplier; 0.5 means twice as fast.</summary>
    public double MachineTime { get; set; } = 1;

    /// <summary>Per-machine time multipliers, by qualified machine ID (e.g. "(BC)12" for the keg); 0 means instant.</summary>
    public Dictionary<string, double> MachineTimeOverrides { get; set; } = new();

    /// <summary>Per-species farm animal settings, by Data/FarmAnimals key (e.g. "White Chicken").</summary>
    public Dictionary<string, AnimalRule> AnimalRules { get; set; } = new();

    /// <summary>The time multiplier for a machine: its override, else the global one.</summary>
    public double MachineTimeFor(string qualifiedMachineId) => this.MachineTimeOverrides.TryGetValue(qualifiedMachineId, out double value) ? value : this.MachineTime;

    /// <summary>Ore multiplier per mine band, keyed by <see cref="MineBands"/>.</summary>
    public Dictionary<string, double> MineOre { get; set; } = new();

    /// <summary>Multiplier on how many tiles of a mine level get a stone.</summary>
    public double MineStones { get; set; } = 1;

    /// <summary>Multiplier on monster spawns in the mines.</summary>
    public double MineMonsters { get; set; } = 1;

    /// <summary>Multiplier on gem nodes in the mines.</summary>
    public double MineGems { get; set; } = 1;

    /// <summary>Whether every mine level gets a ladder down as soon as it's generated.</summary>
    public bool MineAlwaysLadder { get; set; }

    // fishing

    /// <summary>Fish bite right away, are hooked automatically and the minigame is won at once.</summary>
    public bool InstantFishing { get; set; }

    /// <summary>Every catch counts as perfect.</summary>
    public bool PerfectCatch { get; set; }

    /// <summary>Every catch comes with a treasure chest.</summary>
    public bool AlwaysTreasure { get; set; }

    /// <summary>Fish per catch (vanilla 1, 2 with some bait).</summary>
    public int FishPerCatch { get; set; } = 1;

    /// <summary>Forced fish quality (0, 1, 2, 4), or -1 to keep the game's.</summary>
    public int FishQuality { get; set; } = -1;

    /// <summary>Every fish is caught at its species' maximum size.</summary>
    public bool FishMaxSize { get; set; }

    /// <summary>A qualified item ID every cast catches instead of the game's pick, or null.</summary>
    public string? ForcedFishId { get; set; }

    /// <summary>Minimum number of vanilla loot rolls in a fishing treasure chest (vanilla 1, then each extra roll has a 40% chance, 60% for golden chests).</summary>
    public int TreasureRolls { get; set; } = 1;

    /// <summary>Fishing treasure chest stack multiplier.</summary>
    public int TreasureMultiplier { get; set; } = 1;

    /// <summary>Whether <see cref="TreasureLoot"/> replaces the chest's vanilla contents instead of adding to them.</summary>
    public bool TreasureReplaceVanilla { get; set; }

    /// <summary>Extra items rolled into every fishing treasure chest.</summary>
    public List<LootEntry> TreasureLoot { get; set; } = new();

    /// <summary>Changes to fishing spawn tables, by location name in Data/Locations.</summary>
    public Dictionary<string, FishTableEdit> FishTables { get; set; } = new();

    /// <summary>Replacement drop tables, by monster name in Data/Monsters.</summary>
    public Dictionary<string, List<LootEntry>> MonsterDrops { get; set; } = new();

    // loot

    /// <summary>Items picked up in the world (harvest, forage, fish, monster drops) are multiplied by this.</summary>
    public int PickupMultiplier { get; set; } = 1;

    /// <summary>Minimum quality (0, 1, 2 or 4) of crops, fish, animal products and artisan goods picked up.</summary>
    public int MinQuality { get; set; }

    /// <summary>How many times a slain monster rolls its drops (vanilla 1).</summary>
    public int MonsterLootRolls { get; set; } = 1;

    /// <summary>Sell price multiplier for shops and the shipping bin.</summary>
    public double SellPrice { get; set; } = 1;

    // player

    public bool InfiniteHealth { get; set; }

    public bool InfiniteStamina { get; set; }

    /// <summary>The clock doesn't move.</summary>
    public bool FreezeTime { get; set; }

    /// <summary>Every day is the luckiest possible day.</summary>
    public bool MaxDailyLuck { get; set; }

    /// <summary>Extra walking speed (a permanent buff; vanilla coffee is +1).</summary>
    public int SpeedBonus { get; set; }

    /// <summary>Extra item pickup radius in pixels (a permanent buff; vanilla radius is 128).</summary>
    public int MagnetRadiusBonus { get; set; }

    /// <summary>Extra luck level (a permanent buff).</summary>
    public int LuckBonus { get; set; }

    // building & crafting

    /// <summary>Robin's and the Wizard's buildings and Robin's house upgrades cost no gold or materials.</summary>
    public bool FreeBuild { get; set; }

    /// <summary>Buildings and building upgrades finish as soon as they're placed; house upgrades are ready the next morning.</summary>
    public bool InstantBuild { get; set; }

    /// <summary>Crafting and cooking need no ingredients.</summary>
    public bool FreeCrafting { get; set; }

    /// <summary>The mine bands, by the ore they hold.</summary>
    public static readonly string[] MineBands = { "copper", "iron", "gold", "iridium" };

    /// <summary>The band for a mine level: 1-39 copper, 40-79 iron, 80-120 gold, Skull Cavern iridium.</summary>
    public static string BandForLevel(int level) => level switch
    {
        < 40 => "copper",
        < 80 => "iron",
        <= 120 => "gold",
        _ => "iridium",
    };

    public double OreMultiplier(int level) => this.MineOre.TryGetValue(BandForLevel(level), out double value) ? value : 1;
}

/// <summary>Overrides for one farm animal species (Data/FarmAnimals); null keeps the game's value.</summary>
internal sealed class AnimalRule
{
    /// <summary>Days between two produce (1 = every day).</summary>
    public int? DaysToProduce { get; set; }

    /// <summary>Days for a baby to grow up.</summary>
    public int? DaysToMature { get; set; }

    /// <summary>Always give the deluxe produce (large eggs, large milk...) when the species has one.</summary>
    public bool AlwaysDeluxe { get; set; }
}
