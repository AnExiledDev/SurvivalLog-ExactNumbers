using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class CadenceTests
{
    [Fact]
    public void The_first_look_is_due_straight_away()
    {
        Assert.True(new Cadence(1f).IsDue(0f));
    }

    [Fact]
    public void A_look_is_not_due_again_until_the_interval_has_passed()
    {
        var cadence = new Cadence(1f);

        cadence.Ran(10f);

        Assert.False(cadence.IsDue(10.5f));
        Assert.True(cadence.IsDue(11f));
    }

    [Fact]
    public void A_clock_that_went_backwards_does_not_stall_the_looks()
    {
        var cadence = new Cadence(1f);

        cadence.Ran(10f);

        Assert.True(cadence.IsDue(2f));
    }

    /// <summary>At 200 days a float clock moves in steps of 2 seconds, so a quarter second looks like no time at all.</summary>
    [Fact]
    public void A_short_interval_still_works_on_a_clock_far_from_zero()
    {
        const double later = 200 * 24 * 3600;
        var cadence = new Cadence(0.25);

        cadence.Ran(later);

        Assert.False(cadence.IsDue(later + 0.125));
        Assert.True(cadence.IsDue(later + 0.25));
    }
}
