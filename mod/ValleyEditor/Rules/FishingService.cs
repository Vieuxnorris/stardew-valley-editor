using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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

    public FishingService(IModHelper helper, Harmony harmony, IMonitor log)
    {
        monitor = log;
        helper.Events.Display.MenuChanged += OnMenuChanged;

        harmony.Patch(
            AccessTools.Method(typeof(FishingRod), nameof(FishingRod.pullFishFromWater)),
            prefix: new HarmonyMethod(typeof(FishingService), nameof(BeforePullFishFromWater)));
        harmony.Patch(
            AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
            postfix: new HarmonyMethod(typeof(FishingService), nameof(AfterGetFish)));
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

    /// <summary>Fill the fishing treasure chest as it opens.</summary>
    private static void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (e.NewMenu is not ItemGrabMenu { source: FishingChestSource } menu)
            return;
        RulesData rules = RulesService.Current;
        if (rules.TreasureMultiplier <= 1 && rules.TreasureLoot.Count == 0)
            return;

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
        }
        catch (Exception ex)
        {
            monitor?.Log($"Treasure chest rules failed: {ex}", LogLevel.Error);
        }
    }
}
