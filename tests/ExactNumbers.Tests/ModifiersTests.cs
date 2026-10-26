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

    /// <summary>
    /// The game is read into one reused dictionary several times a second; the same ratios as before must be recognised
    /// so the old modifiers are kept, and any changed, new or missing ratio must not be.
    /// </summary>
    [Fact]
    public void Ratios_read_again_are_held_only_when_every_one_is_exactly_the_same()
    {
        var current = Satiety(0.25f);
        var read = new Dictionary<RatioKey, float> { [new RatioKey(Stat.Satiety, false)] = 0.25f };

        Assert.True(current.Holds(read));

        read[new RatioKey(Stat.Satiety, false)] = 0.2500001f;

        Assert.False(current.Holds(read));

        read[new RatioKey(Stat.Satiety, false)] = 0.25f;
        read[new RatioKey(Stat.Stamina, false)] = 0.25f;

        Assert.False(current.Holds(read));

        read.Clear();

        Assert.False(current.Holds(read));
    }

    /// <summary>The dictionary read into is reused for the next read, so modifiers made from it must not change with it.</summary>
    [Fact]
    public void Modifiers_made_from_a_reused_read_keep_their_ratios_when_it_is_read_into_again()
    {
        var read = new Dictionary<RatioKey, float> { [new RatioKey(Stat.Satiety, false)] = 0.25f };
        var made = new Modifiers(read);

        read.Clear();
        read[new RatioKey(Stat.Satiety, false)] = 0.5f;

        Assert.Equal<float?>(0.25f, made.RatioFor(Stat.Satiety, false));
        Assert.False(made.Holds(read));
    }

    [Fact]
    public void A_ratio_that_was_not_read_is_unknown()
    {
        Assert.Null(Satiety(0.1f).RatioFor(Stat.Satiety, true));
        Assert.Equal<float?>(0.1f, Satiety(0.1f).RatioFor(Stat.Satiety, false));
    }
}
