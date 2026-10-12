using ExactNumbers.Core;
using Xunit;
using static ExactNumbers.Tests.Uses;

namespace ExactNumbers.Tests;

/// <summary>The measurer as the hooks drive it: a use starts, the character's changes land, the action is removed.</summary>
public class MeasurerTests
{
    private static readonly MeasureKey BedStamina = new(Bed, Stat.Stamina);

    private readonly List<string> _trace = new();
    private readonly Measurer _measurer;

    public MeasurerTests()
    {
        _measurer = new Measurer(new MeasureStore(), _trace.Add);
    }

    [Fact]
    public void A_completed_use_is_kept_and_its_function_reported_once_so_its_chips_are_resent()
    {
        _measurer.Play(Use());
        _measurer.Cost(Call(-5));
        _measurer.Cost(Call(-5));
        _measurer.End(ActionId, false, 60);

        Assert.Equal(-10, _measurer.Store.Find(BedStamina)!.Low, 6);
        Assert.Equal(new[] { Bed }, _measurer.TakeChanged());
        Assert.Empty(_measurer.TakeChanged());
    }

    /// <summary>The game removes the last action after the next one has started; that must not end the new use.</summary>
    [Fact]
    public void The_end_of_another_action_leaves_the_open_use_alone()
    {
        _measurer.Play(Use(actionId: ActionId));
        _measurer.Cost(Call(-5));

        Assert.Null(_measurer.End(ActionId + 1, false, 30));

        _measurer.Cost(Call(-5));

        Assert.Equal(-10, _measurer.End(ActionId, false, 60)!.Samples.Single().Amount, 6);
        Assert.Contains(_trace, line => line.StartsWith("use end ignored: action 12 ended while action 11", StringComparison.Ordinal));
    }

    [Fact]
    public void A_use_the_player_broke_off_adds_nothing_to_the_store()
    {
        _measurer.Play(Use());
        _measurer.Cost(Call(-5));
        _measurer.End(ActionId, true, 30);

        Assert.Equal(0, _measurer.Store.Count);
        Assert.Empty(_measurer.TakeChanged());
    }

    [Fact]
    public void An_end_with_no_use_open_measures_nothing()
    {
        Assert.Null(_measurer.End(ActionId, false, 30));
        Assert.Contains("use end with no open use: action 11", _trace);
    }

    /// <summary>The trace is for questions about the game later, so it holds calls that measured nothing too.</summary>
    [Fact]
    public void Every_change_of_the_character_is_traced_measured_or_not()
    {
        _measurer.Cost(Call(2, isAction: false));
        _measurer.Cost(Call(-5));

        Assert.Equal(2, _trace.Count);
        Assert.EndsWith("-> NotAction", _trace[0]);
        Assert.EndsWith("-> NoUse", _trace[1]);
    }

    [Fact]
    public void Each_kept_measurement_is_traced_with_what_it_now_holds()
    {
        _measurer.Play(Use());
        _measurer.Cost(Call(-5));
        _measurer.End(ActionId, false, 60);

        Assert.Contains("measured 500/7 Stamina: per use n=1 lo=-5 hi=-5 fill=no endedAtMax=no recent=-5@45", _trace);
    }

    // ===== The end-of-use reward =====

    /// <summary>
    /// A bed's reward for a full night lands after the action's own effects, shaped like no action's change
    /// (RewardSetManager.c:993); while the game is giving it, it is part of what the use did.
    /// </summary>
    [Fact]
    public void The_end_of_use_reward_given_while_the_reward_window_is_open_counts_toward_the_use()
    {
        _measurer.Play(Use());
        _measurer.Cost(Call(10));
        _measurer.OpenReward(ActionId, Bed);

        Assert.Equal(TickVerdict.Reward, _measurer.Cost(Reward(5, before: 60)));

        _measurer.CloseReward();
        _measurer.End(ActionId, false, 60);

        Assert.Equal(15, _measurer.Store.Find(BedStamina)!.Low, 6);
    }

    /// <summary>The same shape comes from quest and event rewards, which have nothing to do with the furniture.</summary>
    [Fact]
    public void A_reward_shaped_change_outside_the_reward_window_does_not_count()
    {
        _measurer.Play(Use());

        Assert.Equal(TickVerdict.NotAction, _measurer.Cost(Reward(5)));

        _measurer.OpenReward(ActionId, Bed);
        _measurer.CloseReward();

        Assert.Equal(TickVerdict.NotAction, _measurer.Cost(Reward(5)));

        _measurer.End(ActionId, false, 60);

        Assert.Equal(0, _measurer.Store.Count);
    }

    /// <summary>A lump at the end of an action that runs until stopped is no part of its rate.</summary>
    [Fact]
    public void The_reward_of_an_action_that_runs_until_stopped_does_not_count_toward_its_rate()
    {
        _measurer.Play(Use(isIndefinite: true));
        _measurer.OpenReward(ActionId, Bed);

        Assert.Equal(TickVerdict.RewardInRate, _measurer.Cost(Reward(5)));
    }

    // ===== Fill-to =====

    /// <summary>The game's fill effect marks the change, so one use is enough to know no single number is right.</summary>
    [Fact]
    public void A_change_made_by_a_fill_effect_marks_the_measurement_fill_to_from_the_first_use()
    {
        _measurer.Play(Use());
        _measurer.Cost(Call(20, before: 60, isFillTo: true));
        _measurer.End(ActionId, false, 60);

        Assert.True(_measurer.Store.Find(BedStamina)!.IsFillTo);
    }

    // ===== One button press =====

    /// <summary>One press of a function that runs two actions: the chip is what the press did, so the two add up.</summary>
    [Fact]
    public void The_actions_of_one_button_press_add_up_to_one_use()
    {
        _measurer.Play(Use(actionId: 11, press: 9));
        _measurer.Cost(Call(-5, stat: Stat.Satiety));
        _measurer.End(11, false, 30, pressGoesOn: true);
        _measurer.Play(Use(actionId: 12, press: 9));
        _measurer.Cost(Call(-7, before: 45, stat: Stat.Satiety));
        _measurer.End(12, false, 60);

        var record = _measurer.Store.Find(new MeasureKey(Bed, Stat.Satiety))!;

        Assert.Equal(1, record.Count);
        Assert.Equal(-12, record.Low, 6);
        Assert.Equal(-12, record.High, 6);
    }

    /// <summary>The game dropped the press's next action, so what the press did is what it did so far.</summary>
    [Fact]
    public void A_press_whose_next_action_never_started_counts_as_it_stands()
    {
        _measurer.Play(Use(actionId: 11, press: 9));
        _measurer.Cost(Call(-5, stat: Stat.Satiety));
        _measurer.End(11, false, 30, pressGoesOn: true);

        Assert.Equal(0, _measurer.Store.Count);

        _measurer.Play(Use(actionId: 20, press: 10));

        Assert.Equal(-5, _measurer.Store.Find(new MeasureKey(Bed, Stat.Satiety))!.Low, 6);
    }
}
