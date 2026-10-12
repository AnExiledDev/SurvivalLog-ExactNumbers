using System.Collections.Generic;

namespace ExactNumbers.Core;

/// <summary>
/// Trace lines waiting to be written. The hooks run on every stat change of the character, so they only add a line
/// here; the lines go to the file in one batch when the measurements are saved. Past MaxLines the rest are counted and
/// dropped, so a burst cannot grow memory between two saves, and the batch says how many were lost.
/// </summary>
public sealed class TraceBuffer
{
    /// <summary>Far more than ten seconds of play makes; about 5 MB of text at the longest lines.</summary>
    public const int MaxLines = 20_000;

    private readonly List<string> _lines = new();
    private int _dropped;

    public void Add(string line)
    {
        if (_lines.Count >= MaxLines)
        {
            _dropped++;

            return;
        }

        _lines.Add(line);
    }

    /// <returns>The lines added since the last call, in order, then a line saying how many were dropped, if any.</returns>
    public IReadOnlyList<string> Take()
    {
        var taken = new List<string>(_lines);

        if (_dropped > 0) taken.Add(TraceText.Dropped(_dropped));

        _lines.Clear();
        _dropped = 0;

        return taken;
    }
}
