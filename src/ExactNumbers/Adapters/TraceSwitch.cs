using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// The trace log, which the TraceLog and Measure settings turn on and off while the game runs. While it is off, new
/// lines are dropped; lines already kept are still written at the next flush. Any thread.
/// </summary>
internal sealed class TraceSwitch
{
    private readonly BufferedTrace _trace;
    private volatile bool _isOn;

    public TraceSwitch(BufferedTrace trace)
    {
        _trace = trace;
    }

    public bool IsOn => _isOn;

    /// <summary>On the main thread, after a change of either setting.</summary>
    public void Use(bool isOn) => _isOn = isOn;

    public void Write(string message)
    {
        if (_isOn) _trace.Write(message);
    }

    public void WriteCost(CostCall call, TickVerdict verdict)
    {
        if (_isOn) _trace.WriteCost(call, verdict);
    }

    /// <summary>Safe on any thread, one at a time.</summary>
    public void Flush() => _trace.Flush();
}
