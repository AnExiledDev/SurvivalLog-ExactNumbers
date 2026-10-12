using System.Globalization;
using ExactNumbers.Core;
using Xunit;
using static ExactNumbers.Tests.Uses;

namespace ExactNumbers.Tests;

/// <summary>The trace log: what each line must hold so a player's log can answer questions about the game, and its size cap.</summary>
public class TraceTests
{
    [Fact]
    public void A_cost_line_holds_everything_the_hooks_saw()
    {
        var line = TraceText.Cost(Call(10, before: 50, funcId: 7, at: 3600), TickVerdict.Kept);

        Assert.Equal(
            "cost attr=Stamina value=10 requested=10 applied=10 before=50 after=60 max=100 reach=60 source=Furniture config=500 target=1 args=Action dep=no action=11 func=7 time=3600 -> Kept",
            line);
    }

    [Fact]
    public void A_cost_line_without_a_change_event_says_so()
    {
        Assert.Contains("requested=none applied=none before=none after=none", TraceText.Cost(Call(-5, hasChange: false), TickVerdict.NoChange));
    }

    /// <summary>Whether the menu message carries function ids at all can only be told from a player's log.</summary>
    [Fact]
    public void A_menu_line_says_for_each_button_what_the_message_and_the_state_held()
    {
        var reads = new[] { new MenuRead(0, null, 301, "@500/301/Satiety:-12"), new MenuRead(1, 302, null, "Stamina:-20") };

        var line = TraceText.Menu("SelectMenu", new MenuState(500, new[] { new MenuButton(301, "Satiety:-12"), new MenuButton(0, null) }), reads);

        Assert.Equal("menu page=SelectMenu furniture=500 state buttons=2 buttons: 0 json=absent state=301 key=@500/301/Satiety:-12 | 1 json=302 state=absent key=Stamina:-20", line);
        Assert.Contains("furniture=no state", TraceText.Menu("SelectMenu", null, reads));
    }

    [Fact]
    public void Trace_numbers_are_written_the_same_in_every_language()
    {
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Contains("value=-2.5 ", TraceText.Cost(Call(-2.5f), TickVerdict.Kept));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void The_log_is_moved_aside_before_a_line_would_take_it_past_its_cap()
    {
        var budget = new LogBudget(0, 100);

        Assert.False(budget.Spend(60));
        Assert.False(budget.Spend(40));
        Assert.True(budget.Spend(1));
        Assert.False(budget.Spend(99));
        Assert.True(budget.Spend(1));
    }

    [Fact]
    public void A_log_carried_over_from_the_last_run_counts_toward_the_cap()
    {
        Assert.True(new LogBudget(95, 100).Spend(10));
    }

    /// <summary>Moving an empty file aside gains nothing, and a line is never split or dropped.</summary>
    [Fact]
    public void A_line_longer_than_the_cap_still_goes_into_a_fresh_log_whole()
    {
        var budget = new LogBudget(0, 100);

        Assert.False(budget.Spend(500));
        Assert.True(budget.Spend(1));
    }
}
