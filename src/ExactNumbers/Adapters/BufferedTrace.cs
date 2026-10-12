using System.Collections.Generic;
using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// The trace log as the hooks see it: a line is stamped and kept in memory, and Flush writes what was kept to the log
/// file in one batch. The session flushes with every measurements save and when the game closes, so a crash loses at
/// most the last few seconds of trace. Lines from the BepInEx log go to the file at once, so the two can interleave
/// out of order in the file; the stamps keep the real order. Never throws.
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
        var line = ExactLog.Stamp(message);

        lock (_gate) _buffer.Add(line);
    }

    public void Flush()
    {
        IReadOnlyList<string> lines;

        lock (_gate) lines = _buffer.Take();

        if (lines.Count > 0) _log.WriteStamped(lines);
    }
}
