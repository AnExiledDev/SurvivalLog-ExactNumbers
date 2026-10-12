using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class ModifiersTests
{
    private static Modifiers Satiety(float costRatio) =>
        new(new Dictionary<RatioKey, float> { [new RatioKey(Stat.Satiety, false)] = costRatio });

    [Fact]
    public void Any_change_in_a_ratio_counts_as_a_change()
    {
        Assert.False(Satiety(0.1f).SameAs(Satiety(0.1000001f)));
        Assert.True(Satiety(0.1f).SameAs(Satiety(0.1f)));
    }

    [Fact]
    public void A_ratio_that_appears_or_disappears_counts_as_a_change()
    {
        Assert.False(Modifiers.None.SameAs(Satiety(0f)));
        Assert.False(Satiety(0f).SameAs(Modifiers.None));
    }

    [Fact]
    public void A_ratio_that_was_not_read_is_unknown()
    {
        Assert.Null(Satiety(0.1f).RatioFor(Stat.Satiety, true));
        Assert.Equal<float?>(0.1f, Satiety(0.1f).RatioFor(Stat.Satiety, false));
    }
}
