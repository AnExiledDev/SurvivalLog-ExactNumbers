using ExactNumbers.Core;
using Xunit;
using static ExactNumbers.Tests.Uses;

namespace ExactNumbers.Tests;

/// <summary>What a chip shows once its furniture function has been measured.</summary>
public class MeasuredChipTests
{
    private const string Minus = "\u2212";

    private static ChipLabel Label(string preview, MeasureStore store, Modifiers? modifiers = null, MeasureSite? site = null) =>
        ChipLabels.For(preview, modifiers ?? Modifiers.None, site ?? Bed, store)!.Last();

    private static ChipLabel Estimate(string vanilla, string listed) => new(ChipKind.Gain, vanilla, ChipLabels.EstimateMark + listed, ChipLabels.EstimateTitle);

    [Fact]
    public void A_measured_fixed_amount_is_shown_as_one_number_without_the_estimate_mark()
    {
        var label = Label("Stamina:+20", Store(Sample(18), Sample(18)));

        Assert.Equal(new ChipLabel(ChipKind.Gain, "++", "+18", "measured over 2 uses, total per use"), label);
    }

    [Fact]
    public void A_function_not_measured_yet_shows_the_games_estimate()
    {
        Assert.Equal(Estimate("++", "+20"), Label("Stamina:+20", new MeasureStore()));
    }

    /// <summary>Two functions can list the same preview and do different things; one's numbers never go on the other's chips.</summary>
    [Fact]
    public void Another_functions_measurement_is_not_used()
    {
        var store = Store(Sample(18, site: new MeasureSite(Furniture, 8)), Sample(18, site: new MeasureSite(501, Func)));

        Assert.Equal(Estimate("++", "+20"), Label("Stamina:+20", store));
    }

    [Fact]
    public void A_chip_whose_function_is_unknown_shows_the_games_estimate()
    {
        var store = Store(Sample(18));

        Assert.Equal(Estimate("++", "+20"), ChipLabels.For("Stamina:+20", Modifiers.None, null, store)!.Single());
    }

    [Fact]
    public void An_amount_that_varied_between_uses_is_shown_as_a_range_from_the_smaller_change()
    {
        Assert.Equal("+12 to +16", Label("Stamina:+20", Store(Sample(16, reach: 70), Sample(12, reach: 40))).Shown);
        Assert.Equal(Minus + "12 to " + Minus + "16", Label("Stamina:-20", Store(Sample(-16, reach: 30), Sample(-12, reach: 40))).Shown);
    }

    [Fact]
    public void An_action_that_runs_until_stopped_shows_its_change_per_in_game_hour()
    {
        var label = Label("Stamina:+20", Store(Sample(4, isRate: true)));

        Assert.Equal("+4" + ChipLabels.PerHourSuffix, label.Shown);
        Assert.Equal("measured over 1 use, per in-game hour", label.Title);
    }

    /// <summary>Measurements are stored before the character's ratio, so a changed ratio changes the number shown.</summary>
    [Fact]
    public void A_measured_amount_is_scaled_by_the_ratio_the_character_has_now()
    {
        var modifiers = new Modifiers(new Dictionary<RatioKey, float> { [new RatioKey(Stat.Stamina, true)] = 0.5f });

        var label = Label("Stamina:+20", Store(Sample(18)), modifiers);

        Assert.Equal("+27", label.Shown);
        Assert.Equal("measured over 1 use, total per use; +18 before your modifiers", label.Title);
    }

    /// <summary>A function that tops a stat up has no single right number; an exact-looking wrong one is worse than the estimate.</summary>
    [Fact]
    public void A_function_that_fills_a_stat_to_a_limit_keeps_the_estimate_and_says_why()
    {
        var store = Store(Sample(12, reach: 60), Sample(16, reach: 100, endedAtMax: true));

        var label = Label("Stamina:+20", store);

        Assert.Equal(ChipLabels.EstimateMark + "+20", label.Shown);
        Assert.Equal(ChipLabels.EstimateTitle + ChipLabels.FillToNote, label.Title);
    }

    /// <summary>
    /// The fill effect is told from the game's own effect, not from amounts that differ, so even one use, or uses that
    /// always start from the same level, keep the estimate.
    /// </summary>
    [Fact]
    public void A_function_seen_filling_a_stat_once_keeps_the_estimate()
    {
        var label = ChipLabels.For("Stamina:+20", Modifiers.None, Bed, Store(Sample(20, reach: 80, isFillTo: true)))!.Last();

        Assert.Equal("~+20", label.Shown);
    }

    [Fact]
    public void A_measurement_going_the_other_way_than_the_chip_keeps_the_estimate()
    {
        Assert.Equal(Estimate("++", "+20"), Label("Stamina:+20", Store(Sample(-5))));
    }

    [Fact]
    public void A_max_chip_keeps_the_games_own_number_when_the_stat_was_measured()
    {
        var labels = ChipLabels.For("MaxStamina:+10;Stamina:+20", Modifiers.None, Bed, Store(Sample(18)))!;

        Assert.Equal(new ChipLabel(ChipKind.Cap, "+10", null, null), labels[0]);
        Assert.Equal("+18", labels[1].Shown);
    }
}
