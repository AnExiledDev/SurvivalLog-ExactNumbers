using ExactNumbers.Core;
using Xunit;
using static ExactNumbers.Tests.Uses;

namespace ExactNumbers.Tests;

/// <summary>Which of the character's stat changes measure a furniture function, and what one use of it measured.</summary>
public class MeasureRulesTests
{
    private static UseOutcome Run(UseStart use, bool isBroken, params CostCall[] calls)
    {
        var tracker = new UseTracker();

        tracker.Start(use);

        foreach (var call in calls) tracker.Tick(call);

        return tracker.End(isBroken)!;
    }

    // ===== Which changes count =====

    [Fact]
    public void An_actions_own_change_sourced_to_furniture_counts()
    {
        Assert.Equal(TickVerdict.Kept, TickRule.Judge(Call(-5)));
    }

    [Fact]
    public void Natural_recovery_does_not_count()
    {
        Assert.Equal(TickVerdict.NotAction, TickRule.Judge(Call(2, isAction: false)));
    }

    [Fact]
    public void Only_changes_the_game_sourced_to_a_piece_of_furniture_count()
    {
        Assert.Equal(TickVerdict.NotFurniture, TickRule.Judge(Call(-5, isFurniture: false)));
        Assert.Equal(TickVerdict.NotFurniture, TickRule.Judge(Call(-5, configId: 0)));
    }

    [Fact]
    public void A_follow_on_change_one_stat_makes_to_another_does_not_count()
    {
        Assert.Equal(TickVerdict.Dependency, TickRule.Judge(Call(-5, isDependency: true)));
    }

    [Fact]
    public void A_change_to_a_stat_no_chip_shows_does_not_count()
    {
        Assert.Equal(TickVerdict.NotChipStat, TickRule.Judge(Call(-5, stat: null)));
    }

    [Fact]
    public void A_call_that_raised_no_change_or_asked_for_nothing_does_not_count()
    {
        Assert.Equal(TickVerdict.NoChange, TickRule.Judge(Call(-5, hasChange: false)));
        Assert.Equal(TickVerdict.Zero, TickRule.Judge(Call(0)));
    }

    /// <summary>A change stopped at zero or at the maximum says nothing about the action's amount.</summary>
    [Fact]
    public void A_change_the_stat_could_not_take_in_full_is_not_a_measurement()
    {
        Assert.Equal(TickVerdict.Clamped, TickRule.Judge(Call(10, before: 95, after: 100)));
        Assert.Equal(TickVerdict.Clamped, TickRule.Judge(Call(-5, before: 3, after: 0)));
    }

    // ===== What a use measured =====

    [Fact]
    public void A_finite_use_that_ran_to_its_end_measures_its_total()
    {
        var outcome = Run(Use(), false, Call(-5), Call(-5), Call(-5));

        var sample = Assert.Single(outcome.Samples);
        Assert.Equal(new MeasureKey(Bed, Stat.Stamina), sample.Key);
        Assert.False(sample.IsRate);
        Assert.Equal(-15, sample.Amount, 6);
    }

    [Fact]
    public void A_finite_use_the_player_broke_off_measures_nothing()
    {
        var outcome = Run(Use(), true, Call(-5), Call(-5));

        Assert.True(outcome.IsDiscarded);
        Assert.Empty(outcome.Samples);
        Assert.Equal(new[] { "500/7 Stamina broken off" }, outcome.Dropped);
    }

    [Fact]
    public void A_stat_that_went_both_up_and_down_in_one_use_is_not_measured()
    {
        var outcome = Run(Use(), false, Call(-5), Call(3));

        Assert.Empty(outcome.Samples);
        Assert.Equal(new[] { "500/7 Stamina went both up and down" }, outcome.Dropped);
    }

    [Fact]
    public void A_finite_use_that_hit_a_limit_measures_nothing_for_that_stat()
    {
        var outcome = Run(Use(), false, Call(10, before: 80), Call(10, before: 95, after: 100), Call(-5, stat: Stat.Satiety));

        var sample = Assert.Single(outcome.Samples);
        Assert.Equal(Stat.Satiety, sample.Key.Stat);
        Assert.Equal(new[] { "500/7 Stamina hit a limit" }, outcome.Dropped);
    }

    /// <summary>Evenly spaced changes give the exact rate: the first change opens the clock, so it is not counted.</summary>
    [Fact]
    public void An_indefinite_use_measures_its_change_per_in_game_hour()
    {
        var outcome = Run(Use(isIndefinite: true), false, Call(2, at: 0), Call(2, at: 1800), Call(2, at: 3600), Call(2, at: 5400));

        var sample = Assert.Single(outcome.Samples);
        Assert.True(sample.IsRate);
        Assert.Equal(4, sample.Amount, 6);
    }

    [Fact]
    public void An_indefinite_use_counts_however_the_player_ends_it()
    {
        var outcome = Run(Use(isIndefinite: true), true, Call(2, at: 0), Call(2, at: 3600));

        Assert.False(outcome.IsDiscarded);
        Assert.Equal(2, Assert.Single(outcome.Samples).Amount, 6);
    }

    [Fact]
    public void An_indefinite_use_shorter_than_an_in_game_hour_measures_no_rate()
    {
        var outcome = Run(Use(isIndefinite: true), false, Call(2, at: 0), Call(2, at: 1800));

        Assert.Empty(outcome.Samples);
        Assert.Equal(new[] { "500/7 Stamina ran under an in-game hour" }, outcome.Dropped);
    }

    [Fact]
    public void An_indefinite_use_keeps_the_rate_it_measured_before_the_stat_hit_its_limit()
    {
        var outcome = Run(
            Use(isIndefinite: true),
            false,
            Call(2, at: 0),
            Call(2, at: 1800),
            Call(2, at: 3600),
            Call(2, before: 99, after: 100, at: 5400),
            Call(2, at: 7200));

        Assert.Equal(4, Assert.Single(outcome.Samples).Amount, 6);
    }

    /// <summary>The game started something else before the use ended, so the use never completed.</summary>
    [Fact]
    public void A_use_still_open_when_another_starts_is_thrown_away()
    {
        var tracker = new UseTracker();
        var first = Use(actionId: 11);

        tracker.Start(first);
        tracker.Tick(Call(-5));

        Assert.Equal(first, tracker.Start(Use(actionId: 12)));
        Assert.Empty(tracker.End(false)!.Samples);
    }

    [Fact]
    public void A_change_while_no_use_is_open_measures_nothing()
    {
        var tracker = new UseTracker();

        Assert.Equal(TickVerdict.NoUse, tracker.Tick(Call(-5)));
        Assert.Null(tracker.End(false));
    }

    [Fact]
    public void A_change_with_no_furniture_function_known_measures_nothing()
    {
        var tracker = new UseTracker();

        tracker.Start(Use(funcId: 0));

        Assert.Equal(TickVerdict.NoFunc, tracker.Tick(Call(-5, funcId: 0)));
    }

    /// <summary>The function the game matched for the running action is read at each change, so it beats the one seen at the start.</summary>
    [Fact]
    public void The_function_matched_when_the_change_lands_wins_over_the_one_seen_at_the_start()
    {
        var outcome = Run(Use(funcId: 7), false, Call(-5, funcId: 9));

        Assert.Equal(new MeasureSite(Furniture, 9), Assert.Single(outcome.Samples).Key.Site);
    }
}
