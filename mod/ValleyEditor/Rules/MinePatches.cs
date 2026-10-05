using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using SObject = StardewValley.Object;

namespace ValleyEditor.Rules;

/// <summary>Harmony patches on mine level generation (MineShaft.populateLevel) for the mine rules.</summary>
internal static class MinePatches
{
    /// <summary>The Quarry Mine, which isn't part of the mine bands.</summary>
    private const int QuarryMineLevel = 77377;

    /// <summary>Ore nodes a level can hold: copper, iron, gold, iridium, radioactive, and the dangerous-mines copper.</summary>
    private static readonly HashSet<string> OreNodes = new() { "(O)751", "(O)290", "(O)764", "(O)765", "(O)95", "(O)849" };

    /// <summary>Plain stones that <see cref="MineShaft.getAppropriateOre"/> returns a quarter of the time.</summary>
    private static readonly HashSet<string> PlainStones = new() { "(O)668", "(O)670" };

    private static IMonitor? monitor;

    public static void Apply(Harmony harmony, IMonitor log)
    {
        monitor = log;
        harmony.Patch(
            AccessTools.Method(typeof(MineShaft), "adjustLevelChances"),
            postfix: new HarmonyMethod(typeof(MinePatches), nameof(AfterAdjustLevelChances)));
        harmony.Patch(
            AccessTools.Method(typeof(MineShaft), "populateLevel"),
            postfix: new HarmonyMethod(typeof(MinePatches), nameof(AfterPopulateLevel)));
    }

    /// <summary>Scale the per-tile chances the level generator rolls against. Parameter names must match the original.</summary>
    private static void AfterAdjustLevelChances(ref double stoneChance, ref double monsterChance, ref double gemStoneChance)
    {
        RulesData rules = RulesService.Current;
        stoneChance = Math.Min(0.95, stoneChance * rules.MineStones);
        monsterChance *= rules.MineMonsters;
        gemStoneChance *= rules.MineGems;
    }

    /// <summary>Convert stones to ore (or back) once the level is generated, using the game's own ore choice for the level.</summary>
    private static void AfterPopulateLevel(MineShaft __instance)
    {
        try
        {
            if (__instance.mineLevel == QuarryMineLevel)
                return;
            if (RulesService.Current.MineAlwaysLadder)
                AddLadder(__instance);

            double multiplier = RulesService.Current.OreMultiplier(__instance.mineLevel);
            if (Math.Abs(multiplier - 1) < 1e-9)
                return;

            var (stoneToOre, oreToStone) = RuleMath.OreConversion(multiplier);
            Random random = __instance.mineRandom;
            foreach (var (tile, obj) in __instance.objects.Pairs.ToArray())
            {
                SObject? replacement = null;
                if (OreNodes.Contains(obj.QualifiedItemId))
                {
                    if (oreToStone > 0 && random.NextDouble() < oreToStone)
                        replacement = new SObject(random.Choose("668", "670"), 1) { MinutesUntilReady = 2 };
                }
                else if (stoneToOre > 0 && obj.IsBreakableStone() && random.NextDouble() < stoneToOre)
                    replacement = PickOre(__instance, tile);

                if (replacement != null)
                {
                    __instance.objects.Remove(tile);
                    __instance.objects.Add(tile, replacement);
                }
            }
        }
        catch (Exception ex)
        {
            monitor?.Log($"Failed applying the mine ore rule on level {__instance.mineLevel}: {ex}", LogLevel.Error);
        }
    }

    /// <summary>Put a ladder down on a free tile, unless the level has one or can't have one (elevator floors, monster-gated levels).</summary>
    private static void AddLadder(MineShaft mine)
    {
        if (mine.ladderHasSpawned || mine.mineLevel % 5 == 0 || mine.mustKillAllMonstersToAdvance())
            return;

        var size = mine.Map.Layers[0].LayerSize;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int x = mine.mineRandom.Next(size.Width), y = mine.mineRandom.Next(size.Height);
            if (mine.isTileClearForMineObjects(x, y))
            {
                mine.createLadderDown(x, y);
                return;
            }
        }
    }

    /// <summary>The level's ore, retrying a few times since a quarter of the game's picks are plain stone.</summary>
    private static SObject? PickOre(MineShaft mine, Vector2 tile)
    {
        for (int i = 0; i < 6; i++)
        {
            SObject ore = mine.getAppropriateOre(tile);
            if (!PlainStones.Contains(ore.QualifiedItemId))
                return ore;
        }
        return null;
    }
}
