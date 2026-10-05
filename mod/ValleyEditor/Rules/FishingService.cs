using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace ValleyEditor.Rules;

/// <summary>Fishing catch and treasure rules: fish per catch, size, quality, forced fish, treasure chest contents.</summary>
internal sealed class FishingService
{
    /// <summary>ItemGrabMenu.source for fishing treasure chests.</summary>
    private const int FishingChestSource = ItemGrabMenu.source_fishingChest;

    /// <summary>The chest menu's grid (an InventoryMenu with default capacity) shows 36 slots.</summary>
    private const int MaxChestSlots = 36;

    private static IMonitor? monitor;

    /// <summary>Loot rolls done in the treasure chest being filled.</summary>
    private static int treasureRollsDone;

    /// <summary>How many decay constants the transpiler replaced (2 expected: regular and golden chests).</summary>
    private static int decayConstantsPatched;

    public FishingService(IModHelper helper, Harmony harmony, IMonitor log)
    {
        monitor = log;
        harmony.Patch(
            AccessTools.Method(typeof(FishingRod), nameof(FishingRod.openTreasureMenuEndFunction)),
            prefix: new HarmonyMethod(typeof(FishingService), nameof(BeforeOpenTreasureMenu)),
            postfix: new HarmonyMethod(typeof(FishingService), nameof(AfterOpenTreasureMenu)),
            transpiler: new HarmonyMethod(typeof(FishingService), nameof(TranspileTreasureRolls)));
        if (decayConstantsPatched != 2)
            log.Log($"Treasure roll patch matched {decayConstantsPatched} of the 2 expected constants; the chest item count rule may not work with this game version.", LogLevel.Warn);
        harmony.Patch(
            AccessTools.Method(typeof(FishingRod), nameof(FishingRod.pullFishFromWater)),
            prefix: new HarmonyMethod(typeof(FishingService), nameof(BeforePullFishFromWater)));
        harmony.Patch(
            AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
            postfix: new HarmonyMethod(typeof(FishingService), nameof(AfterGetFish)));
    }

    /// <summary>Merge identical stacks, then drop at the player's feet whatever still doesn't fit in the chest's 36 slots.</summary>
    private static void FitInChest(IList<Item> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = items.Count - 1; j > i; j--)
            {
                if (items[i].canStackWith(items[j]) && items[i].Stack + items[j].Stack <= items[i].maximumStackSize())
                {
                    items[i].Stack += items[j].Stack;
                    items.RemoveAt(j);
                }
            }
        }

        while (items.Count > MaxChestSlots)
        {
            Item extra = items[^1];
            items.RemoveAt(items.Count - 1);
            Game1.createItemDebris(extra, Game1.player.getStandingPosition(), Game1.player.FacingDirection);
        }
    }

    /// <summary>The largest size (inches) of a fish in Data/Fish, if it's a rod fish.</summary>
    public static int? MaxSize(string fishId)
    {
        string id = fishId.StartsWith("(O)") ? fishId[3..] : fishId;
        if (!DataLoader.Fish(Game1.content).TryGetValue(id, out string? raw))
            return null;
        string[] fields = raw.Split('/');
        // crab pot entries read 'name/trap/...' and have no size range at this position
        return fields.Length > 4 && fields[1] != "trap" && int.TryParse(fields[4], out int max) ? max : null;
    }

    /// <summary>Change the catch just before the game hands it to the player. Parameter names must match the original.</summary>
    private static void BeforePullFishFromWater(string fishId, ref int fishSize, ref int fishQuality, ref int numCaught)
    {
        try
        {
            RulesData rules = RulesService.Current;
            if (rules.FishPerCatch > 1)
                numCaught = Math.Max(numCaught, rules.FishPerCatch);
            if (rules.FishQuality >= 0)
                fishQuality = rules.FishQuality; // a perfect catch can still raise it, as in vanilla
            if (rules.FishMaxSize && MaxSize(fishId) is int max)
                fishSize = max;
        }
        catch (Exception ex)
        {
            monitor?.LogOnce($"Fish catch rules failed: {ex}", LogLevel.Error);
        }
    }

    /// <summary>Replace whatever the game picked with the forced fish.</summary>
    private static void AfterGetFish(ref Item __result)
    {
        string? forced = RulesService.Current.ForcedFishId;
        if (string.IsNullOrEmpty(forced))
            return;
        Item? fish = ItemRegistry.Create(forced, allowNull: true);
        if (fish != null)
            __result = fish;
    }

    /// <summary>
    /// The chest loop is `while (random &lt;= chance) { chance *= golden ? 0.6f : 0.4f; ...add loot... }` with chance starting at 1.
    /// Route both constants through <see cref="TreasureDecay"/>, which keeps the chance at 1 until the minimum rolls are done.
    /// </summary>
    private static IEnumerable<CodeInstruction> TranspileTreasureRolls(IEnumerable<CodeInstruction> instructions)
    {
        var decay = AccessTools.Method(typeof(FishingService), nameof(TreasureDecay));
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && instruction.operand is float value && (value == 0.4f || value == 0.6f))
            {
                decayConstantsPatched++;
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, decay);
            }
        }
    }

    private static void BeforeOpenTreasureMenu() => treasureRollsDone = 0;

    /// <summary>The factor applied to the chance of another roll: 1 (certain) until the minimum is reached, then vanilla.</summary>
    private static float TreasureDecay(float vanilla)
    {
        treasureRollsDone++;
        return treasureRollsDone < RulesService.Current.TreasureRolls ? 1f : vanilla;
    }

    /// <summary>Fill the fishing treasure chest right after the game opens it (FishingRod.openTreasureMenuEndFunction ends by setting the menu).</summary>
    private static void AfterOpenTreasureMenu()
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu { source: FishingChestSource } menu)
        {
            monitor?.Log("Treasure chest rules: the fishing chest menu wasn't open after openTreasureMenuEndFunction.", LogLevel.Trace);
            return;
        }
        RulesData rules = RulesService.Current;
        if (rules.TreasureMultiplier <= 1 && rules.TreasureLoot.Count == 0)
        {
            FitInChest(menu.ItemsToGrabMenu.actualInventory);
            monitor?.Log($"Treasure chest: {treasureRollsDone} rolls, {menu.ItemsToGrabMenu.actualInventory.Count} items.", LogLevel.Trace);
            return;
        }

        try
        {
            IList<Item> items = menu.ItemsToGrabMenu.actualInventory;
            if (rules.TreasureReplaceVanilla && rules.TreasureLoot.Count > 0)
                items.Clear();

            foreach (LootEntry entry in rules.TreasureLoot)
            {
                if (items.Count >= MaxChestSlots)
                    break;
                if (Game1.random.NextDouble() >= entry.Chance)
                    continue;
                int stack = Game1.random.Next(entry.MinStack, Math.Max(entry.MinStack, entry.MaxStack) + 1);
                Item? item = ItemRegistry.Create(entry.ItemId, stack, entry.Quality, allowNull: true);
                if (item != null)
                    items.Add(item);
            }

            if (rules.TreasureMultiplier > 1)
            {
                foreach (Item item in items)
                {
                    if (item.maximumStackSize() > 1)
                        item.Stack = (int)Math.Min(item.maximumStackSize(), (long)item.Stack * rules.TreasureMultiplier);
                }
            }
            FitInChest(items);
            monitor?.Log($"Treasure chest rules applied: {treasureRollsDone} rolls, ×{rules.TreasureMultiplier}, {rules.TreasureLoot.Count} table entries, {items.Count} items now.", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            monitor?.Log($"Treasure chest rules failed: {ex}", LogLevel.Error);
        }
    }
}
