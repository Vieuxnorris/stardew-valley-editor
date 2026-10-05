using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace ValleyEditor.Rules;

/// <summary>Machine helpers shared by the editor's API and the instant-machines rule.</summary>
internal static class MachineTools
{
    /// <summary>The modData key holding a single machine's output override (saved with the machine).</summary>
    private const string OutputKey = "Julien.ValleyEditor/Output";

    /// <summary>The machine's own output override, if any.</summary>
    public static OutputOverride? GetOwnOverride(SObject machine)
    {
        if (!machine.modData.TryGetValue(OutputKey, out string? json))
            return null;
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<OutputOverride>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void SetOwnOverride(SObject machine, OutputOverride? rule)
    {
        if (rule is null)
            machine.modData.Remove(OutputKey);
        else
            machine.modData[OutputKey] = Newtonsoft.Json.JsonConvert.SerializeObject(rule);
    }

    /// <summary>The override that applies: the machine's own, else its type's.</summary>
    public static OutputOverride? GetOverride(SObject machine)
    {
        return GetOwnOverride(machine)
            ?? (RulesService.Current.MachineOutputRules.TryGetValue(machine.QualifiedItemId, out OutputOverride? rule) ? rule : null);
    }

    /// <summary>Apply the output override to what the machine holds (after the game made a new batch).</summary>
    public static void ApplyOverride(SObject machine)
    {
        if (machine.heldObject.Value is not { } output || GetOverride(machine) is not { } rule)
            return;
        if (!string.IsNullOrEmpty(rule.ItemId) && rule.ItemId != output.QualifiedItemId && ItemRegistry.Create(rule.ItemId, allowNull: true) is SObject replacement)
        {
            replacement.Stack = output.Stack;
            replacement.Quality = output.Quality;
            machine.heldObject.Value = output = replacement;
        }
        if (rule.Stack is int stack)
            output.Stack = System.Math.Clamp(stack, 1, output.maximumStackSize());
        if (rule.Quality is int quality)
            output.Quality = quality;
    }

    public static bool IsMachine(SObject obj) => obj.GetMachineData() != null;

    /// <summary>Whether the machine is processing something that isn't ready yet.</summary>
    public static bool IsWorking(SObject obj) => obj.heldObject.Value != null && !obj.readyForHarvest.Value;

    /// <summary>Make a working machine ready now, the way the clock does (Object.minutesElapsed), even for overnight-only machines.</summary>
    public static bool Finish(SObject obj)
    {
        if (!IsWorking(obj))
            return false;
        obj.MinutesUntilReady = 0;
        obj.minutesElapsed(0);
        if (!obj.readyForHarvest.Value)
        {
            // OnlyCompleteOvernight machines skip the above outside the new day
            MachineData? data = obj.GetMachineData();
            obj.readyForHarvest.Value = true;
            obj.showNextIndex.Value = data?.ShowNextIndexWhenReady ?? false;
        }
        return true;
    }
}
