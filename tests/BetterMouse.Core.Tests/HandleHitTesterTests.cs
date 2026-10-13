using System.Collections.Generic;
using BetterMouse.Core;
using Xunit;

namespace BetterMouse.Core.Tests;

public class HandleHitTesterTests
{
    private sealed record Item(string Id);

    [Fact]
    public void NoCandidateWithinRadius_ReturnsNullAndResetsLast()
    {
        var a = new Item("a");
        Item? last = a;

        var hit = HandleHitTester.Pick(new List<(Item, double)> { (a, 50) }, 10, ref last);

        Assert.Null(hit);
        Assert.Null(last);
    }

    [Fact]
    public void SingleCandidate_IsStableAcrossRepeatedPicks()
    {
        var a = new Item("a");
        Item? last = null;
        var candidates = new List<(Item, double)> { (a, 5) };

        Assert.Equal(a, HandleHitTester.Pick(candidates, 10, ref last));
        Assert.Equal(a, HandleHitTester.Pick(candidates, 10, ref last));
    }

    [Fact]
    public void CoincidentCandidates_CycleOnRepeatedPicks()
    {
        var a = new Item("a");
        var b = new Item("b");
        Item? last = null;
        var candidates = new List<(Item, double)> { (a, 5), (b, 5) };

        Assert.Equal(a, HandleHitTester.Pick(candidates, 10, ref last));
        Assert.Equal(b, HandleHitTester.Pick(candidates, 10, ref last));
        Assert.Equal(a, HandleHitTester.Pick(candidates, 10, ref last));
    }

    [Fact]
    public void NearbyButSeparateCandidates_NeverStealFromNearest()
    {
        var a = new Item("a");
        var b = new Item("b");
        Item? last = null;
        var candidates = new List<(Item, double)> { (a, 5), (b, 9) };

        Assert.Equal(a, HandleHitTester.Pick(candidates, 12, ref last));
        Assert.Equal(a, HandleHitTester.Pick(candidates, 12, ref last));
    }

    [Fact]
    public void MovingOntoASeparateHandle_PicksItDespiteRememberedLast()
    {
        var a = new Item("a");
        var b = new Item("b");
        Item? last = null;

        // Cursor over a.
        Assert.Equal(a, HandleHitTester.Pick(
            new List<(Item, double)> { (a, 5), (b, 9) }, 12, ref last));

        // Cursor moves onto b; the remembered a must not cause b to be skipped.
        Assert.Equal(b, HandleHitTester.Pick(
            new List<(Item, double)> { (a, 9), (b, 1) }, 12, ref last));
    }
}
