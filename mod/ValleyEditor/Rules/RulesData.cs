using System.Collections.Generic;

namespace ValleyEditor.Rules;

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
