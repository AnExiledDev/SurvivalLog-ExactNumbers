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
            "cost attr=Stamina value=10 requested=10 applied=10 before=50 after=60 max=100 reach=60 source=Furniture config=500 target=1 args=Action dep=no action=11 func=7 time=3600 fill=no -> Kept",
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

        budget.Spend(60);

        Assert.False(budget.IsFull(40));

        budget.Spend(40);

        Assert.True(budget.IsFull(1));

        budget.Rotated();

        Assert.False(budget.IsFull(99));

        budget.Spend(99);

        Assert.True(budget.IsFull(2));
    }

    [Fact]
    public void A_log_carried_over_from_the_last_run_counts_toward_the_cap()
    {
        Assert.True(new LogBudget(95, 100).IsFull(10));
    }

    /// <summary>Moving an empty file aside gains nothing, and a line is never split.</summary>
    [Fact]
    public void A_line_longer_than_the_cap_still_goes_into_a_fresh_log_whole()
    {
        var budget = new LogBudget(0, 100);

        Assert.False(budget.IsFull(500));

        budget.Spend(500);

        Assert.True(budget.IsFull(1));
    }

    /// <summary>A log that cannot be moved aside must not grow without bound: it stays full, and a move that keeps failing stops it.</summary>
    [Fact]
    public void A_log_that_could_not_be_moved_aside_stays_full_and_stops_after_repeated_failures()
    {
        var budget = new LogBudget(100, 100);

        for (var i = 1; i < LogBudget.MaxFailedRotations; i++)
        {
            budget.RotationFailed();

            Assert.True(budget.IsFull(1));
            Assert.False(budget.IsStopped);
        }

        budget.RotationFailed();

        Assert.True(budget.IsStopped);
    }

    [Fact]
    public void A_move_that_works_again_clears_the_failures_before_it()
    {
        var budget = new LogBudget(100, 100);

        for (var i = 1; i < LogBudget.MaxFailedRotations; i++) budget.RotationFailed();

        budget.Rotated();
        budget.RotationFailed();

        Assert.False(budget.IsStopped);
    }

    /// <summary>Trace lines wait in memory until the next save; a burst cannot grow that without bound, and the log says what was lost.</summary>
    [Fact]
    public void Trace_lines_come_out_in_order_and_a_burst_past_the_cap_says_how_many_were_dropped()
    {
        var buffer = new TraceBuffer();

        for (var i = 0; i < TraceBuffer.MaxLines + 2; i++) buffer.Add("line " + i);

        var taken = buffer.Take();

        Assert.Equal(TraceBuffer.MaxLines + 1, taken.Count);
        Assert.Equal("line 0", taken[0].Message);
        Assert.Equal("line " + (TraceBuffer.MaxLines - 1), taken[taken.Count - 2].Message);
        Assert.Equal("trace: 2 lines dropped, more than 20000 came between two saves", taken[taken.Count - 1].Message);
        Assert.Empty(buffer.Take());
    }

    /// <summary>
    /// A cost call is kept as it is and its line built when the batch is written, off the game's thread; the line must be
    /// the one the trace wrote at once before, made from the call as it was.
    /// </summary>
    [Fact]
    public void A_kept_cost_call_is_written_as_the_same_line_it_would_have_been_at_once()
    {
        var call = Call(10, before: 50, funcId: 7, at: 3600);
        var buffer = new TraceBuffer();

        buffer.Add(TraceEntry.Cost(new DateTime(2026, 10, 26, 5, 0, 0, DateTimeKind.Utc), call, TickVerdict.Clamped));

        Assert.Equal(TraceText.Cost(call, TickVerdict.Clamped), buffer.Take().Single().Message);
    }

    /// <summary>The trace is taken at every save; one with nothing in it must not make a new list each time.</summary>
    [Fact]
    public void Taking_an_empty_trace_hands_back_the_shared_empty_list()
    {
        var buffer = new TraceBuffer();

        Assert.Same(Array.Empty<TraceEntry>(), buffer.Take());

        buffer.Add("line");

        Assert.Equal("line", buffer.Take().Single().Message);
        Assert.Same(Array.Empty<TraceEntry>(), buffer.Take());
    }

    /// <summary>
    /// The measurer's cost trace goes to the entry taker when it has one, so no line is built on the game's thread, and to
    /// the plain line trace otherwise.
    /// </summary>
    [Fact]
    public void A_measurer_hands_cost_calls_to_the_entry_taker_unbuilt_and_builds_lines_only_without_one()
    {
        var lines = new List<string>();
        var calls = new List<(CostCall Call, TickVerdict Verdict)>();
        var withTaker = new Measurer(new MeasureStore(), lines.Add, (call, verdict) => calls.Add((call, verdict)));
        var call = Call(-5);

        withTaker.Cost(call);

        Assert.Empty(lines);
        Assert.Same(call, calls.Single().Call);
        Assert.Equal(TickVerdict.NoUse, calls.Single().Verdict);

        var withoutTaker = new Measurer(new MeasureStore(), lines.Add);

        withoutTaker.Cost(call);

        Assert.Equal(TraceText.Cost(call, TickVerdict.NoUse), lines.Single());
    }
}
