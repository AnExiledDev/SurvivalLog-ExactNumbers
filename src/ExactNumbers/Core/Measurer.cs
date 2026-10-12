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
    private readonly HashSet<MeasureSite> _changed = new();

    /// <param name="trace">Where trace lines go; null for none.</param>
    public Measurer(MeasureStore store, Action<string>? trace = null)
    {
        _store = store;
        _trace = trace ?? (_ => { });
    }

    public MeasureStore Store => _store;

    public TickVerdict Cost(CostCall call)
    {
        var verdict = _tracker.Tick(call);

        _trace(TraceText.Cost(call, verdict));

        return verdict;
    }

    public void Play(UseStart use)
    {
        var open = _tracker.Start(use);

        if (open != null) _trace(TraceText.Discarded(open));

        _trace(TraceText.Start(use));
    }

    /// <summary>
    /// The character's action ended. The end of another action than the open use's (one the game removes after the next
    /// has started) leaves the open use alone.
    /// </summary>
    /// <param name="actionId">The action config id the character's action component held when the action was removed.</param>
    /// <param name="at">The in-game clock in seconds, -1 when unknown; for the trace only.</param>
    /// <returns>Null when no use ended.</returns>
    public UseOutcome? End(int actionId, bool isBroken, int at)
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

        var outcome = _tracker.End(isBroken);

        if (outcome == null) return null;

        _trace(TraceText.End(outcome, at));

        if (!outcome.IsDiscarded) Keep(outcome.Samples);

        return outcome;
    }

    /// <returns>The furniture functions with new measurements since the last call.</returns>
    public IReadOnlyList<MeasureSite> TakeChanged()
    {
        var changed = _changed.ToList();

        _changed.Clear();

        return changed;
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
