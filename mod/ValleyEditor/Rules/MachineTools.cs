using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace ValleyEditor.Rules;

/// <summary>Machine helpers shared by the editor's API and the instant-machines rule.</summary>
internal static class MachineTools
{
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
