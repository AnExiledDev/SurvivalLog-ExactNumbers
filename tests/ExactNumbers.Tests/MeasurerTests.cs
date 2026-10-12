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
}
