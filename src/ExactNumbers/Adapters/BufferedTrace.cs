using System;
using System.Collections.Generic;
using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// The trace log as the hooks see it: an entry is timed and kept in memory, and Flush builds the lines and writes them
/// to the log file in one batch. The session flushes on the file worker with every measurements save and when the game
/// closes, so a crash loses at most the last few seconds of trace. Lines from the BepInEx log go to the file at once, so
/// the two can interleave out of order in the file; the stamps keep the real order. Never throws.
/// </summary>
internal sealed class BufferedTrace
{
    private readonly ExactLog _log;
    private readonly TraceBuffer _buffer = new();
    private readonly object _gate = new();

    public BufferedTrace(ExactLog log)
    {
        _log = log;
    }

    public void Write(string message)
    {
        var entry = TraceEntry.Line(DateTime.UtcNow, message);

        lock (_gate) _buffer.Add(entry);
    }

    /// <summary>A cost call, kept as it is; its long line is built at Flush.</summary>
    public void WriteCost(CostCall call, TickVerdict verdict)
    {
        var entry = TraceEntry.Cost(DateTime.UtcNow, call, verdict);

        lock (_gate) _buffer.Add(entry);
    }

    /// <summary>Safe on any thread, one at a time.</summary>
    public void Flush()
    {
        IReadOnlyList<TraceEntry> entries;

        lock (_gate) entries = _buffer.Take();

        if (entries.Count == 0) return;

        var lines = new string[entries.Count];

        for (var i = 0; i < lines.Length; i++) lines[i] = Line(entries[i]);

        _log.WriteStamped(lines);
    }

    private static string Line(TraceEntry entry)
    {
        try
        {
            return entry.At is { } at ? ExactLog.Stamp(at.ToLocalTime(), entry.Message) : entry.Message;
        }
        catch (Exception ex)
        {
            return "trace: a line could not be built: " + ex.Message;
        }
    }
}
