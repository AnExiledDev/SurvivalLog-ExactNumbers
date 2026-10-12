using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class ChipLabelsTests
{
    private const string Minus = "\u2212";

    private static Modifiers Ratios(params (Stat Stat, bool IsGain, float Ratio)[] ratios) =>
        new(ratios.ToDictionary(ratio => new RatioKey(ratio.Stat, ratio.IsGain), ratio => ratio.Ratio));

    [Fact]
    public void Without_modifiers_each_chip_shows_its_listed_number_marked_as_an_estimate()
    {
        var labels = ChipLabels.For("Satiety:-12;Morale:+4", Modifiers.None);

        Assert.Equal(new[]
        {
            new ChipLabel(ChipKind.Cost, Minus + Minus, "~" + Minus + "12", ChipLabels.EstimateTitle),
            new ChipLabel(ChipKind.Gain, "+", "~+4", ChipLabels.EstimateTitle),
        }, labels);
    }

    [Fact]
    public void A_number_made_from_the_preview_is_never_shown_without_the_estimate_mark()
    {
        var modifiers = Ratios((Stat.Satiety, false, 0.25f), (Stat.Stamina, false, 0f));

        var shown = ChipLabels.For("Satiety:-12;Stamina:-20;Morale:+4;MaxVitality:+10", modifiers)!.Where(label => label.Shown != null).ToList();

        Assert.Equal(3, shown.Count);
        Assert.All(shown, label => Assert.StartsWith(ChipLabels.EstimateMark, label.Shown));
        Assert.All(shown, label => Assert.StartsWith(ChipLabels.EstimateTitle, label.Title));
    }

    [Fact]
    public void A_decrease_is_scaled_by_the_cost_ratio_and_keeps_its_listed_number_in_the_hover_text()
    {
        var modifiers = Ratios((Stat.Satiety, false, 0.25f), (Stat.Satiety, true, 1f));

        var label = Assert.Single(ChipLabels.For("Satiety:-12", modifiers)!);

        Assert.Equal(new ChipLabel(ChipKind.Cost, Minus + Minus, "~" + Minus + "15", ChipLabels.EstimateTitle + "; listed " + Minus + "12 before your modifiers"), label);
    }

    [Fact]
    public void An_increase_is_scaled_by_the_gain_ratio()
    {
        var modifiers = Ratios((Stat.Morale, true, 0.5f), (Stat.Morale, false, 3f));

        var label = Assert.Single(ChipLabels.For("Morale:+4", modifiers)!);

        Assert.Equal("~+6", label.Shown);
        Assert.EndsWith("listed +4 before your modifiers", label.Title);
    }

    [Fact]
    public void A_ratio_of_one_stat_does_not_touch_another_stat()
    {
        var modifiers = Ratios((Stat.Stamina, false, 1f));

        var label = Assert.Single(ChipLabels.For("Satiety:-12", modifiers)!);

        Assert.Equal("~" + Minus + "12", label.Shown);
    }

    [Fact]
    public void A_zero_ratio_names_no_listed_number_because_nothing_changed()
    {
        var modifiers = Ratios((Stat.Satiety, false, 0f));

        var label = Assert.Single(ChipLabels.For("Satiety:-12", modifiers)!);

        Assert.Equal("~" + Minus + "12", label.Shown);
        Assert.Equal(ChipLabels.EstimateTitle, label.Title);
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
