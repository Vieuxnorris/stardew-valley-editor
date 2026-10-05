using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Machines;
using StardewValley.GameData.WildTrees;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace ValleyEditor.Rules;

/// <summary>A crop's growth data as other mods left it, before our edit.</summary>
internal sealed record CropBaseline(List<int> Phases, int RegrowDays, string HarvestItemId);

/// <summary>Holds the current save's rules and applies them: data edits, fruit tree growth, and (via <see cref="MinePatches"/>) mine generation.</summary>
internal sealed class RulesService
{
    private const string SaveKey = "rules";
    private static readonly string[] EditedAssets = { "Data/Crops", "Data/Machines", "Data/WildTrees" };

    private readonly IModHelper helper;

    /// <summary>The rules for the loaded save (vanilla when none is loaded). Only touched on the game thread.</summary>
    public static RulesData Current { get; private set; } = new();

    /// <summary>Crop data by seed ID as other mods left it, captured each time Data/Crops is edited.</summary>
    public Dictionary<string, CropBaseline> CropBaselines { get; } = new();

    public RulesService(IModHelper helper)
    {
        this.helper = helper;
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.Replace(helper.Data.ReadSaveData<RulesData>(SaveKey) ?? new RulesData());
        helper.Events.GameLoop.Saving += (_, _) => helper.Data.WriteSaveData(SaveKey, Current);
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.Replace(new RulesData());
        helper.Events.GameLoop.DayStarted += (_, _) => GrowFruitTrees();
    }

    /// <summary>Change the rules and reload the data they affect.</summary>
    public void Update(Action<RulesData> change)
    {
        change(Current);
        this.Invalidate();
    }

    public void Replace(RulesData rules)
    {
        Current = rules;
        this.Invalidate();
    }

    /// <summary>Recalculate the growth phases of crops already planted, so new crop rules apply to them too.</summary>
    /// <returns>The number of crops updated.</returns>
    public static int ApplyToPlantedCrops()
    {
        int count = 0;
        Utility.ForEachLocation(location =>
        {
            foreach (TerrainFeature feature in location.terrainFeatures.Values)
            {
                if (feature is HoeDirt dirt)
                    count += Reset(dirt);
            }
            foreach (StardewValley.Object obj in location.objects.Values)
            {
                if (obj is IndoorPot pot)
                    count += Reset(pot.hoeDirt.Value);
            }
            return true;
        });
        return count;

        static int Reset(HoeDirt? dirt)
        {
            if (dirt?.crop is not { } crop || crop.dead.Value)
                return 0;
            crop.ResetPhaseDays();
            dirt.applySpeedIncreases(Game1.player); // re-add fertilizer and Agriculturist bonuses
            return 1;
        }
    }

    private void Invalidate()
    {
        foreach (string asset in EditedAssets)
            this.helper.GameContent.InvalidateCache(asset);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // Late priority: apply on top of content packs (PPJA, etc.) so multipliers scale their crops too
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Crops"))
            e.Edit(asset => this.EditCrops(asset.AsDictionary<string, CropData>().Data), AssetEditPriority.Late);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Machines"))
            e.Edit(asset => EditMachines(asset.AsDictionary<string, MachineData>().Data), AssetEditPriority.Late);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/WildTrees"))
            e.Edit(asset => EditWildTrees(asset.AsDictionary<string, WildTreeData>().Data), AssetEditPriority.Late);
    }

    private void EditCrops(IDictionary<string, CropData> crops)
    {
        RulesData rules = Current;
        this.CropBaselines.Clear();
        foreach ((string seedId, CropData crop) in crops)
        {
            this.CropBaselines[seedId] = new CropBaseline(crop.DaysInPhase.ToList(), crop.RegrowDays, crop.HarvestItemId);

            double multiplier = rules.CropGrowthOverrides.TryGetValue(seedId, out double custom) ? custom : rules.CropGrowth;
            if (Math.Abs(multiplier - 1) < 1e-9)
                continue;
            crop.DaysInPhase = RuleMath.ScalePhases(crop.DaysInPhase, multiplier);
            crop.RegrowDays = RuleMath.ScaleDays(crop.RegrowDays, multiplier);
        }
    }

    private static void EditMachines(IDictionary<string, MachineData> machines)
    {
        double multiplier = Current.MachineTime;
        if (Math.Abs(multiplier - 1) < 1e-9)
            return;

        foreach (MachineData machine in machines.Values)
        {
            foreach (MachineOutputRule rule in machine.OutputRules ?? Enumerable.Empty<MachineOutputRule>())
            {
                rule.MinutesUntilReady = RuleMath.ScaleMinutes(rule.MinutesUntilReady, multiplier);
                rule.DaysUntilReady = RuleMath.ScaleDays(rule.DaysUntilReady, multiplier);
            }
        }
    }

    private static void EditWildTrees(IDictionary<string, WildTreeData> trees)
    {
        double multiplier = Current.WildTreeGrowth;
        if (Math.Abs(multiplier - 1) < 1e-9)
            return;

        foreach (WildTreeData tree in trees.Values)
        {
            tree.GrowthChance = Math.Clamp(tree.GrowthChance * (float)multiplier, 0, 1);
            tree.FertilizedGrowthChance = Math.Clamp(tree.FertilizedGrowthChance * (float)multiplier, 0, 1);
        }
    }

    /// <summary>Young fruit trees count down one day per night; give them the extra days of the speed rule.</summary>
    private static void GrowFruitTrees()
    {
        int extraDays = Current.FruitTreeSpeed - 1;
        if (extraDays <= 0)
            return;

        Utility.ForEachLocation(location =>
        {
            foreach (TerrainFeature feature in location.terrainFeatures.Values)
            {
                if (feature is FruitTree tree && tree.daysUntilMature.Value > 0)
                {
                    tree.daysUntilMature.Value = Math.Max(0, tree.daysUntilMature.Value - extraDays);
                    tree.growthStage.Value = FruitTree.DaysUntilMatureToGrowthStage(tree.daysUntilMature.Value);
                }
            }
            return true;
        });
    }
}
