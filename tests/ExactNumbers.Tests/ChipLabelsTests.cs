using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class ChipLabelsTests
{
    private static Modifiers Ratios(params (Stat Stat, bool IsGain, float Ratio)[] ratios) =>
        new(ratios.ToDictionary(ratio => new RatioKey(ratio.Stat, ratio.IsGain), ratio => ratio.Ratio));

    [Fact]
    public void Without_modifiers_each_chip_shows_its_listed_number()
    {
        var labels = ChipLabels.For("Satiety:-12;Morale:+4", Modifiers.None);

        Assert.Equal(new[]
        {
            new ChipLabel(ChipKind.Cost, "\u2212\u2212", "\u221212", null),
            new ChipLabel(ChipKind.Gain, "+", "+4", null),
        }, labels);
    }

    [Fact]
    public void A_decrease_is_scaled_by_the_cost_ratio_and_keeps_its_base()
    {
        var modifiers = Ratios((Stat.Satiety, false, 0.25f), (Stat.Satiety, true, 1f));

        var label = Assert.Single(ChipLabels.For("Satiety:-12", modifiers)!);

        Assert.Equal(new ChipLabel(ChipKind.Cost, "\u2212\u2212", "\u221215", "\u221212"), label);
    }

    [Fact]
    public void An_increase_is_scaled_by_the_gain_ratio()
    {
        var modifiers = Ratios((Stat.Morale, true, 0.5f), (Stat.Morale, false, 3f));

        var label = Assert.Single(ChipLabels.For("Morale:+4", modifiers)!);

        Assert.Equal("+6", label.Shown);
        Assert.Equal("+4", label.Base);
    }

    [Fact]
    public void A_ratio_of_one_stat_does_not_touch_another_stat()
    {
        var modifiers = Ratios((Stat.Stamina, false, 1f));

        var label = Assert.Single(ChipLabels.For("Satiety:-12", modifiers)!);

        Assert.Equal("\u221212", label.Shown);
    }

    [Fact]
    public void A_zero_ratio_gives_no_base_because_nothing_changed()
    {
        var modifiers = Ratios((Stat.Satiety, false, 0f));

        var label = Assert.Single(ChipLabels.For("Satiety:-12", modifiers)!);

        Assert.Equal("\u221212", label.Shown);
        Assert.Null(label.Base);
    }

    [Fact]
    public void Cap_chips_are_left_as_the_page_draws_them()
    {
        var modifiers = Ratios((Stat.Vitality, true, 1f));

        var label = Assert.Single(ChipLabels.For("MaxVitality:+10", modifiers)!);

        Assert.Equal(new ChipLabel(ChipKind.Cap, "+10", null, null), label);
    }

    [Fact]
    public void A_preview_that_cannot_be_matched_gives_no_labels()
    {
        Assert.Null(ChipLabels.For("Satiety:-99999999999", Modifiers.None));
    }
}
