namespace ExactNumbers.Core;

/// <summary>Runs a periodic look at most once per interval, with the clock passed in.</summary>
public sealed class Cadence
{
    private readonly double _intervalSeconds;
    private double? _lastAt;

    /// <summary>Seconds as a double: a float clock loses its fractions after a few days of play.</summary>
    public Cadence(double intervalSeconds)
    {
        _intervalSeconds = intervalSeconds;
    }

    public bool IsDue(double now) => _lastAt is not { } last || now - last >= _intervalSeconds || now < last;

    public void Ran(double now) => _lastAt = now;
}
