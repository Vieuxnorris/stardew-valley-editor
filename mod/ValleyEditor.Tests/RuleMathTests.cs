using System.Linq;
using ValleyEditor.Rules;
using Xunit;

namespace ValleyEditor.Tests;

public class RuleMathTests
{
    [Fact]
    public void ScalePhases_KeepsPhasesAtMultiplierOne()
    {
        Assert.Equal(new[] { 1, 2, 2, 2 }, RuleMath.ScalePhases(new[] { 1, 2, 2, 2 }, 1));
    }

    [Fact]
    public void ScalePhases_HalvesParsnipToTwoDays()
    {
        // parsnip: 4 phases of 1 day; the game skips 0-day phases
        var phases = RuleMath.ScalePhases(new[] { 1, 1, 1, 1 }, 0.5);
        Assert.Equal(2, phases.Sum());
        Assert.Equal(4, phases.Count);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 2, 2 }, 0.5, 4)] // cauliflower-like: 7 days -> 3.5 -> 4
    [InlineData(new[] { 1, 2, 2, 2 }, 2.0, 14)]
    [InlineData(new[] { 2, 3, 4, 3, 3 }, 0.1, 2)] // 15 days -> 1.5 -> 2
    [InlineData(new[] { 1, 1, 1, 1 }, 0.05, 1)] // never below 1 day
    public void ScalePhases_HitsTargetTotal(int[] phases, double multiplier, int expectedTotal)
    {
        var scaled = RuleMath.ScalePhases(phases, multiplier);
        Assert.Equal(expectedTotal, scaled.Sum());
        Assert.Equal(phases.Length, scaled.Count);
        Assert.All(scaled, days => Assert.True(days >= 0));
    }

    [Fact]
    public void ScalePhases_SpreadsProportionally()
    {
        Assert.Equal(new[] { 2, 4, 4, 4 }, RuleMath.ScalePhases(new[] { 1, 2, 2, 2 }, 2));
    }

    [Fact]
    public void ScalePhases_NeverAllZero()
    {
        var scaled = RuleMath.ScalePhases(new[] { 1, 1, 1, 1, 1, 1 }, 0.05);
        Assert.Equal(1, scaled.Sum());
    }

    [Theory]
    [InlineData(-1, 0.5, -1)] // "no regrowth" stays as is
    [InlineData(0, 0.5, 0)]
    [InlineData(4, 0.5, 2)]
    [InlineData(1, 0.1, 1)]
    [InlineData(3, 2, 6)]
    public void ScaleDays(int days, double multiplier, int expected)
    {
        Assert.Equal(expected, RuleMath.ScaleDays(days, multiplier));
    }

    [Theory]
    [InlineData(-1, 0.5, -1)]
    [InlineData(60, 0.5, 30)]
    [InlineData(30, 0.1, 10)] // at least one clock tick
    [InlineData(4000, 0.25, 1000)]
    [InlineData(45, 1.0, 50)] // rounded to the 10-minute clock
    public void ScaleMinutes(int minutes, double multiplier, int expected)
    {
        Assert.Equal(expected, RuleMath.ScaleMinutes(minutes, multiplier));
    }

    [Fact]
    public void OreConversion_VanillaDoesNothing()
    {
        Assert.Equal((0d, 0d), RuleMath.OreConversion(1));
    }

    [Fact]
    public void OreConversion_MoreOreConvertsStones()
    {
        var (stoneToOre, oreToStone) = RuleMath.OreConversion(3);
        Assert.Equal(0.058, stoneToOre, 6);
        Assert.Equal(0, oreToStone);
    }

    [Fact]
    public void OreConversion_CapsAtEveryStone()
    {
        Assert.Equal(1, RuleMath.OreConversion(50).StoneToOre);
    }

    [Fact]
    public void OreConversion_LessOreConvertsOreBack()
    {
        var (stoneToOre, oreToStone) = RuleMath.OreConversion(0.25);
        Assert.Equal(0, stoneToOre);
        Assert.Equal(0.75, oreToStone, 6);
        Assert.Equal(1, RuleMath.OreConversion(0).OreToStone);
    }
}
