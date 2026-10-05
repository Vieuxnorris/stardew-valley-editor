using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewValley;
using ValleyEditor.Rules;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Per-save game rules: crop, tree and machine speed, and mine generation.</summary>
internal sealed class RulesDomain : Domain
{
    private readonly RulesService rules;
    private readonly CheatsService cheats;

    /// <summary>On/off rules, by JSON field name.</summary>
    private static readonly (string Name, Action<RulesData, bool> Set)[] Switches =
    {
        ("mineAlwaysLadder", (r, v) => r.MineAlwaysLadder = v),
        ("instantFishing", (r, v) => r.InstantFishing = v),
        ("perfectCatch", (r, v) => r.PerfectCatch = v),
        ("alwaysTreasure", (r, v) => r.AlwaysTreasure = v),
        ("infiniteHealth", (r, v) => r.InfiniteHealth = v),
        ("infiniteStamina", (r, v) => r.InfiniteStamina = v),
        ("freezeTime", (r, v) => r.FreezeTime = v),
        ("maxDailyLuck", (r, v) => r.MaxDailyLuck = v),
        ("freeBuild", (r, v) => r.FreeBuild = v),
        ("instantBuild", (r, v) => r.InstantBuild = v),
        ("freeCrafting", (r, v) => r.FreeCrafting = v),
        ("infiniteReach", (r, v) => r.InfiniteReach = v),
        ("placeAnywhere", (r, v) => r.PlaceAnywhere = v),
    };

    /// <summary>Whole-number rules, by JSON field name, with their allowed range.</summary>
    private static readonly (string Name, int Min, int Max, Action<RulesData, int> Set)[] Counts =
    {
        ("pickupMultiplier", 1, 100, (r, v) => r.PickupMultiplier = v),
        ("monsterLootRolls", 1, 20, (r, v) => r.MonsterLootRolls = v),
        ("speedBonus", 0, 20, (r, v) => r.SpeedBonus = v),
        ("magnetRadiusBonus", 0, 2000, (r, v) => r.MagnetRadiusBonus = v),
        ("luckBonus", 0, 20, (r, v) => r.LuckBonus = v),
    };

    public RulesDomain(GameThreadDispatcher game, EditorState state, RulesService rules, CheatsService cheats)
        : base(game, state)
    {
        this.rules = rules;
        this.cheats = cheats;
    }

    public override void Register(Router router)
    {
        router.Get("/api/rules", _ => this.Read(this.Snapshot));

        // partial update: only the fields sent change; a crop override of null removes it
        router.Patch("/api/rules", request =>
        {
            JObject body = request.BodyObject;
            double? cropGrowth = OptDouble(body, "cropGrowth", 0.05, 10);
            int? fruitTreeSpeed = OptInt(body, "fruitTreeSpeed", 1, 28);
            double? wildTreeGrowth = OptDouble(body, "wildTreeGrowth", 0, 20);
            double? machineTime = OptDouble(body, "machineTime", 0, 10);
            double? mineStones = OptDouble(body, "mineStones", 0, 5);
            double? mineMonsters = OptDouble(body, "mineMonsters", 0, 10);
            double? mineGems = OptDouble(body, "mineGems", 0, 20);
            double? sellPrice = OptDouble(body, "sellPrice", 0.01, 100);
            int? minQuality = OptInt(body, "minQuality", 0, 4);
            if (minQuality == 3)
                throw new ApiException(400, "'minQuality' must be 0, 1, 2 or 4.");

            var switches = new List<(Action<RulesData, bool> Set, bool Value)>();
            foreach (var (name, set) in Switches)
            {
                JToken? token = body[name];
                if (token is null || token.Type == JTokenType.Null)
                    continue;
                if (token.Type != JTokenType.Boolean)
                    throw new ApiException(400, $"'{name}' must be true or false.");
                switches.Add((set, token.Value<bool>()));
            }

            var counts = new List<(Action<RulesData, int> Set, int Value)>();
            foreach (var (name, min, max, set) in Counts)
            {
                if (OptInt(body, name, min, max) is int value)
                    counts.Add((set, value));
            }

            var mineOre = new Dictionary<string, double>();
            if (body["mineOre"] is JObject ore)
            {
                foreach (var (band, _) in ore)
                {
                    if (!RulesData.MineBands.Contains(band))
                        throw new ApiException(400, $"Unknown mine band '{band}'. Expected: {string.Join(", ", RulesData.MineBands)}.");
                    mineOre[band] = OptDouble(ore, band, 0, 50) ?? 1;
                }
            }

            var cropOverrides = new Dictionary<string, double?>();
            if (body["cropGrowthOverrides"] is JObject overrides)
            {
                foreach (var (seedId, _) in overrides)
                    cropOverrides[seedId] = OptDouble(overrides, seedId, 0.05, 10);
            }

            return this.Write(() =>
            {
                this.rules.Update(r =>
                {
                    r.CropGrowth = cropGrowth ?? r.CropGrowth;
                    r.FruitTreeSpeed = fruitTreeSpeed ?? r.FruitTreeSpeed;
                    r.WildTreeGrowth = wildTreeGrowth ?? r.WildTreeGrowth;
                    r.MachineTime = machineTime ?? r.MachineTime;
                    r.MineStones = mineStones ?? r.MineStones;
                    r.MineMonsters = mineMonsters ?? r.MineMonsters;
                    r.MineGems = mineGems ?? r.MineGems;
                    r.SellPrice = sellPrice ?? r.SellPrice;
                    r.MinQuality = minQuality ?? r.MinQuality;
                    foreach (var (set, value) in switches)
                        set(r, value);
                    foreach (var (set, value) in counts)
                        set(r, value);
                    foreach (var (band, value) in mineOre)
                        r.MineOre[band] = value;
                    foreach (var (seedId, value) in cropOverrides)
                    {
                        if (value.HasValue)
                            r.CropGrowthOverrides[seedId] = value.Value;
                        else
                            r.CropGrowthOverrides.Remove(seedId);
                    }
                });
                this.cheats.ApplyDailyEffects();
                return this.Snapshot();
            });
        });

        router.Post("/api/rules/reset", _ => this.Write(() =>
        {
            this.rules.Replace(new RulesData());
            this.cheats.ApplyDailyEffects();
            return this.Snapshot();
        }));

        router.Post("/api/rules/apply-to-planted-crops", _ => this.Write(() => new { Updated = RulesService.ApplyToPlantedCrops() }));
    }

    private object Snapshot()
    {
        RulesData current = RulesService.Current;

        // read Data/Crops so the baselines are captured even if nothing requested it since the last change
        Dictionary<string, StardewValley.GameData.Crops.CropData> crops = DataLoader.Crops(Game1.content);

        return new
        {
            Rules = current,
            MineBands = RulesData.MineBands,
            Crops = crops
                .Select(pair =>
                {
                    this.rules.CropBaselines.TryGetValue(pair.Key, out CropBaseline? baseline);
                    return new
                    {
                        SeedId = pair.Key,
                        Name = ItemRegistry.GetData("(O)" + pair.Value.HarvestItemId)?.DisplayName
                            ?? ItemRegistry.GetData("(O)" + pair.Key)?.DisplayName
                            ?? pair.Key,
                        HarvestItemId = pair.Value.HarvestItemId != null ? "(O)" + pair.Value.HarvestItemId : null,
                        BaseDays = baseline?.Phases.Sum(),
                        Days = pair.Value.DaysInPhase.Sum(),
                        BaseRegrowDays = baseline?.RegrowDays,
                        RegrowDays = pair.Value.RegrowDays,
                        Override = current.CropGrowthOverrides.TryGetValue(pair.Key, out double value) ? value : (double?)null,
                    };
                })
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
        };
    }

    private static double? OptDouble(JObject body, string name, double min, double max)
    {
        JToken? token = body[name];
        if (token is null || token.Type == JTokenType.Null)
            return null;
        if (token.Type is not (JTokenType.Float or JTokenType.Integer))
            throw new ApiException(400, $"'{name}' must be a number.");

        double value = token.Value<double>();
        if (double.IsNaN(value) || value < min || value > max)
            throw new ApiException(400, $"'{name}' must be between {min} and {max}.");
        return value;
    }
}
