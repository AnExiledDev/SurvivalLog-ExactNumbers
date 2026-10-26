using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// Turns what the hooks see into measurements: every cost call of the player's character goes to the open use, and a
/// use that ends adds its samples to the store. Every step is traced. Not thread safe; the hooks hold one lock around it.
/// </summary>
public sealed class Measurer
{
    private readonly UseTracker _tracker = new();
    private readonly MeasureStore _store;
    private readonly Action<string> _trace;
    private readonly Action<CostCall, TickVerdict>? _traceCost;
    private readonly HashSet<MeasureSite> _changed = new();

    /// <param name="trace">Where trace lines go; null for none, and then no cost line is even built.</param>
    /// <param name="traceCost">
    /// Where cost calls go to be traced, when given: the call is handed over as it is and its line is built later, by
    /// whoever writes the trace. Without it a cost line is built here and goes to trace.
    /// </param>
    public Measurer(MeasureStore store, Action<string>? trace = null, Action<CostCall, TickVerdict>? traceCost = null)
    {
        _store = store;
        _trace = trace ?? (_ => { });
        _traceCost = trace == null ? null : traceCost ?? ((call, verdict) => trace(TraceText.Cost(call, verdict)));
    }

    public MeasureStore Store => _store;

    public TickVerdict Cost(CostCall call)
    {
        var verdict = _tracker.Tick(call);

        // Called for every stat change of the character: the line is long, so it is built only for a trace.
        _traceCost?.Invoke(call, verdict);

        return verdict;
    }

    public void Play(UseStart use)
    {
        if (_tracker.Held is { } held && !use.Continues(held) && _tracker.FinishHeld() is { } finished) Ended(finished, use.At);

        var isContinued = _tracker.Held != null;
        var open = _tracker.Start(use);

        if (open != null) _trace(TraceText.Discarded(open));

        _trace(isContinued ? TraceText.Continued(use) : TraceText.Start(use));
    }

    /// <summary>The end-of-use reward of the action actionId is being given; see UseTracker.OpenReward.</summary>
    public void OpenReward(int actionId, MeasureSite site)
    {
        _tracker.OpenReward(actionId, site);
        _trace(TraceText.RewardOpened(actionId, site));
    }

    public void CloseReward()
    {
        _tracker.CloseReward();
        _trace(TraceText.RewardClosed);
    }

    /// <summary>
    /// The character's action ended. The end of another action than the open use's (one the game removes after the next
    /// has started) leaves the open use alone.
    /// </summary>
    /// <param name="actionId">The action config id the character's action component held when the action was removed.</param>
    /// <param name="at">The in-game clock in seconds, -1 when unknown; for the trace only.</param>
    /// <param name="pressGoesOn">True when the next queued action belongs to the same button press; see UseTracker.End.</param>
    /// <returns>Null when no use ended.</returns>
    public UseOutcome? End(int actionId, bool isBroken, int at, bool pressGoesOn = false)
    {
        if (_tracker.Current is not { } open)
        {
            _trace(TraceText.EndWithoutUse(actionId));

            return null;
        }

        if (open.ActionId != actionId)
        {
            _trace(TraceText.EndIgnored(actionId, open.ActionId));

            return null;
        }

        var outcome = _tracker.End(isBroken, pressGoesOn);

        if (outcome != null) Ended(outcome, at);

        return outcome;
    }

    /// <returns>The furniture functions with new measurements since the last call.</returns>
    /// <remarks>Called every frame: with nothing new it returns the one shared empty list and allocates nothing.</remarks>
    public IReadOnlyList<MeasureSite> TakeChanged()
    {
        if (_changed.Count == 0) return Array.Empty<MeasureSite>();

        var changed = _changed.ToList();

        _changed.Clear();

        return changed;
    }

    private void Ended(UseOutcome outcome, int at)
    {
        _trace(TraceText.End(outcome, at));

        if (!outcome.IsDiscarded) Keep(outcome.Samples);
    }

    private void Keep(IEnumerable<UseSample> samples)
    {
        foreach (var sample in samples)
        {
            var record = _store.Add(sample);

            if (record == null) continue;

            _changed.Add(sample.Key.Site);
            _trace(TraceText.Measured(record));
        }
    }
}
