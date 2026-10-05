using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Tools;
using ValleyEditor.Domains;
using SObject = StardewValley.Object;

namespace ValleyEditor.Rules;

/// <summary>The "broken" rules that act every tick or on events: fishing, loot, god mode, buffs.</summary>
internal sealed class CheatsService
{
    private const string BuffId = "Julien.ValleyEditor.Rules";

    /// <summary>The luckiest daily luck the game rolls (Game1.DailyLuck tops out at 0.1; 0.125 also beats "spirits very happy").</summary>
    private const double MaxLuck = 0.125;

    /// <summary>Ticks after an editor change during which inventory changes are the editor's own, not pickups.</summary>
    private const int EditorGraceTicks = 2;

    /// <summary>Categories where quality stars mean something: crops, flowers, forage, fish, animal products, artisan goods, syrups.</summary>
    private static readonly int[] QualityCategories =
    {
        SObject.VegetableCategory, SObject.FruitsCategory, SObject.flowersCategory, SObject.GreensCategory, SObject.FishCategory,
        SObject.EggCategory, SObject.MilkCategory, SObject.sellAtPierresAndMarnies, SObject.artisanGoodsCategory, SObject.syrupCategory,
    };

    private readonly EditorState state;
    private readonly IMonitor monitor;

    /// <summary>The stack size we last set on an item, so the change we cause isn't multiplied again next tick.</summary>
    private readonly ConditionalWeakTable<Item, StrongBox<int>> stacksWeSet = new();

    private static bool inExtraMonsterDrop;

    public CheatsService(IModHelper helper, EditorState state, Harmony harmony, IMonitor monitor)
    {
        this.state = state;
        this.monitor = monitor;

        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.DayStarted += (_, _) => this.ApplyDailyEffects();
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.ApplyDailyEffects();
        helper.Events.Player.InventoryChanged += this.OnInventoryChanged;

        harmony.Patch(
            AccessTools.Method(typeof(GameLocation), nameof(GameLocation.monsterDrop)),
            postfix: new HarmonyMethod(typeof(CheatsService), nameof(AfterMonsterDrop)));
        harmony.Patch(
            AccessTools.Method(typeof(SObject), nameof(SObject.sellToStorePrice)),
            postfix: new HarmonyMethod(typeof(CheatsService), nameof(AfterSellToStorePrice)));
    }

    /// <summary>Re-apply the effects that the game resets daily or that are set once (luck, buffs). Call on the game thread after rules change.</summary>
    public void ApplyDailyEffects()
    {
        if (!Context.IsWorldReady)
            return;
        RulesData rules = RulesService.Current;
        Farmer player = Game1.player;

        if (rules.MaxDailyLuck)
            player.team.sharedDailyLuck.Value = MaxLuck;

        player.buffs.Remove(BuffId);
        if (rules.SpeedBonus != 0 || rules.MagnetRadiusBonus != 0 || rules.LuckBonus != 0)
        {
            var effects = new BuffEffects();
            effects.Speed.Value = rules.SpeedBonus;
            effects.MagneticRadius.Value = rules.MagnetRadiusBonus;
            effects.LuckLevel.Value = rules.LuckBonus;
            player.applyBuff(new Buff(BuffId, displayName: "Valley Editor", duration: Buff.ENDLESS, effects: effects) { visible = false });
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;
        RulesData rules = RulesService.Current;
        Farmer player = Game1.player;

        if (rules.InfiniteHealth && player.health < player.maxHealth)
            player.health = player.maxHealth;
        if (rules.InfiniteStamina && player.Stamina < player.MaxStamina)
            player.Stamina = player.MaxStamina;
        if (rules.FreezeTime)
            Game1.gameTimeInterval = 0;

        try
        {
            this.UpdateFishing(rules, player);
        }
        catch (Exception ex)
        {
            this.monitor.LogOnce($"Fishing rules failed: {ex}", LogLevel.Error);
        }
    }

    private void UpdateFishing(RulesData rules, Farmer player)
    {
        if (Game1.activeClickableMenu is BobberBar bar)
        {
            if (rules.PerfectCatch)
                bar.perfect = true;
            if (rules.AlwaysTreasure)
            {
                bar.treasure = true;
                bar.treasureCaught = true;
            }
            if (rules.InstantFishing)
                bar.distanceFromCatching = 1.5f; // the minigame checks >= 1 after its own update
            return;
        }

        if (!rules.InstantFishing || player.CurrentTool is not FishingRod rod || !rod.isFishing)
            return;

        // make the fish bite on the next update...
        if (rod.timeUntilFishingBite > 0 && !rod.isNibbling)
            rod.fishingBiteAccumulator = rod.timeUntilFishingBite;

        // ...and hook it like the Auto-Hook enchantment does (FishingRod.tickUpdate)
        else if (rod.isNibbling && !rod.isReeling && !rod.hit && !rod.pullingOutOfWater && !player.UsingTool)
        {
            rod.timePerBobberBob = 1f;
            rod.timeUntilFishingNibbleDone = FishingRod.maxTimeToNibble;
            rod.DoFunction(player.currentLocation, (int)rod.bobber.X, (int)rod.bobber.Y, 1, player);
        }
    }

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        RulesData rules = RulesService.Current;
        if (!e.IsLocalPlayer || (rules.PickupMultiplier <= 1 && rules.MinQuality <= 0))
            return;

        // only things picked up in the world: not shops, chests or crafting (menus), nor the editor's own changes
        if (Game1.activeClickableMenu != null || this.state.Tick - this.state.LastWriteTick <= EditorGraceTicks)
            return;

        foreach (Item item in e.Added)
        {
            if (rules.MinQuality > 0 && item is SObject obj && QualityCategories.Contains(obj.Category) && obj.Quality < rules.MinQuality)
                obj.Quality = rules.MinQuality;
            this.Multiply(item, item.Stack, rules.PickupMultiplier);
        }

        foreach (ItemStackSizeChange change in e.QuantityChanged)
        {
            if (change.NewSize <= change.OldSize)
                continue;
            if (this.stacksWeSet.TryGetValue(change.Item, out StrongBox<int>? ours) && ours.Value == change.NewSize)
                continue; // that's our own multiplication from last tick
            this.Multiply(change.Item, change.NewSize - change.OldSize, rules.PickupMultiplier);
        }
    }

    private void Multiply(Item item, int gained, int multiplier)
    {
        if (multiplier <= 1 || gained <= 0)
            return;
        int target = (int)Math.Min(item.maximumStackSize(), item.Stack + (long)gained * (multiplier - 1));
        if (target <= item.Stack)
            return;
        item.Stack = target;
        this.stacksWeSet.AddOrUpdate(item, new StrongBox<int>(target));
    }

    /// <summary>Roll a slain monster's drops again for the extra loot rolls.</summary>
    private static void AfterMonsterDrop(GameLocation __instance, Monster monster, int x, int y, Farmer who)
    {
        int extra = RulesService.Current.MonsterLootRolls - 1;
        if (extra <= 0 || inExtraMonsterDrop)
            return;

        inExtraMonsterDrop = true;
        try
        {
            for (int i = 0; i < extra; i++)
                __instance.monsterDrop(monster, x, y, who);
        }
        finally
        {
            inExtraMonsterDrop = false;
        }
    }

    private static void AfterSellToStorePrice(ref int __result)
    {
        double multiplier = RulesService.Current.SellPrice;
        if (Math.Abs(multiplier - 1) > 1e-9 && __result > 0)
            __result = (int)Math.Min(int.MaxValue, Math.Round(__result * multiplier));
    }
}
