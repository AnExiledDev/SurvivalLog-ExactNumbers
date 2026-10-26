using System;
using System.Collections.Generic;

namespace ExactNumbers.Core;

/// <summary>
/// One trace line waiting to be written. A cost call keeps the call itself (it is immutable) and its line is built only
/// when the batch is written, off the game's thread. At is when it happened, in UTC; null for a line with no time.
/// </summary>
public readonly record struct TraceEntry(DateTime? At, string? Text, CostCall? Call, TickVerdict Verdict)
{
    public static TraceEntry Line(DateTime at, string text) => new(at, text, null, default);

    public static TraceEntry Cost(DateTime at, CostCall call, TickVerdict verdict) => new(at, null, call, verdict);

    /// <summary>The line without its time.</summary>
    public string Message => Text ?? (Call == null ? "" : TraceText.Cost(Call, Verdict));
}

/// <summary>
/// Trace lines waiting to be written. The hooks run on every stat change of the character, so they only add an entry
/// here; the lines go to the file in one batch when the measurements are saved. Past MaxLines the rest are counted and
/// dropped, so a burst cannot grow memory between two saves, and the batch says how many were lost.
/// </summary>
public sealed class TraceBuffer
{
    /// <summary>Far more than ten seconds of play makes; about 5 MB of text at the longest lines.</summary>
    public const int MaxLines = 20_000;

    private List<TraceEntry> _entries = new();
    private int _dropped;

    public void Add(TraceEntry entry)
    {
        if (_entries.Count >= MaxLines)
        {
            _dropped++;

            return;
        }

        _entries.Add(entry);
    }

    /// <summary>A line with no time, as tests and the dropped count add them.</summary>
    public void Add(string line) => Add(new TraceEntry(null, line, null, default));

    /// <returns>
    /// The entries added since the last call, in order, then one with no time saying how many were dropped, if any. The
    /// list is handed over, not copied; with nothing added it is the shared empty list.
    /// </returns>
    public IReadOnlyList<TraceEntry> Take()
    {
        if (_entries.Count == 0 && _dropped == 0) return Array.Empty<TraceEntry>();

        var taken = _entries;

        if (_dropped > 0) taken.Add(new TraceEntry(null, TraceText.Dropped(_dropped), null, default));

        // Sized like the last batch, so a steady trace does not regrow its list from empty every time.
        _entries = new List<TraceEntry>(Math.Min(taken.Count, MaxLines));
        _dropped = 0;

        return taken;
    }
}
