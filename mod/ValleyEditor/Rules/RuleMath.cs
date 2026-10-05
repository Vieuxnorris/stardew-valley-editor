using System;
using System.Collections.Generic;
using System.Linq;

namespace ValleyEditor.Rules;

/// <summary>Pure calculations behind the rules, kept free of game types so they can be unit tested.</summary>
internal static class RuleMath
{
    /// <summary>
    /// Scale a crop's growth phases so their total is <paramref name="multiplier"/> times the original (at least 1 day).
    /// Days are spread over phases in proportion to the original (largest remainder), so a phase can drop to 0 days;
    /// the game skips 0-day phases (Crop.newDay), which is what lets a 4×1-day parsnip grow in 2 days.
    /// </summary>
    public static List<int> ScalePhases(IReadOnlyList<int> phases, double multiplier)
    {
        if (phases.Count == 0 || Math.Abs(multiplier - 1) < 1e-9)
            return phases.ToList();

        int originalTotal = phases.Sum();
        if (originalTotal <= 0)
            return phases.ToList();

        int target = Math.Max(1, (int)Math.Round(originalTotal * multiplier, MidpointRounding.AwayFromZero));
        double[] exact = phases.Select(days => (double)days * target / originalTotal).ToArray();
        int[] result = exact.Select(value => (int)Math.Floor(value)).ToArray();

        // hand out the days lost to rounding down, biggest fractions first (earlier phases win ties)
        int missing = target - result.Sum();
        foreach (int i in Enumerable.Range(0, phases.Count).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i).Take(missing))
            result[i]++;

        // a crop must spend at least a day somewhere before the last phase is reached
        if (result.All(days => days == 0))
            result[0] = 1;
        return result.ToList();
    }

    /// <summary>Scale a day count (regrowth, machine days), keeping it at least 1. Non-positive values mean "not set" and are kept.</summary>
    public static int ScaleDays(int days, double multiplier)
    {
        return days <= 0 ? days : Math.Max(1, (int)Math.Round(days * multiplier, MidpointRounding.AwayFromZero));
    }

    /// <summary>Scale in-game minutes, rounded to the clock's 10-minute steps and at least 10. Non-positive values are kept.</summary>
    public static int ScaleMinutes(int minutes, double multiplier)
    {
        if (minutes <= 0)
            return minutes;
        int scaled = (int)Math.Round(minutes * multiplier / 10, MidpointRounding.AwayFromZero) * 10;
        return Math.Max(10, scaled);
    }

    /// <summary>
    /// How to reach <paramref name="multiplier"/> times the vanilla ore on a mine level by converting nodes.
    /// Vanilla makes about <paramref name="vanillaOreShare"/> of stones into ore, so for ×N we convert that share
    /// times (N - 1) of plain stones into ore; below ×1 we turn (1 - N) of the ore back into plain stone.
    /// </summary>
    /// <returns>The chance to convert each plain stone to ore, and each ore node to plain stone.</returns>
    public static (double StoneToOre, double OreToStone) OreConversion(double multiplier, double vanillaOreShare = 0.029)
    {
        if (multiplier >= 1)
            return (Math.Min(1, vanillaOreShare * (multiplier - 1)), 0);
        return (0, Math.Clamp(1 - multiplier, 0, 1));
    }
}
