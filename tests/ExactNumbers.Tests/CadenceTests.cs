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
}
