namespace ExactNumbers.Core;

/// <summary>Runs a periodic look at most once per interval, with the clock passed in.</summary>
public sealed class Cadence
{
    private readonly float _intervalSeconds;
    private float? _lastAt;

    public Cadence(float intervalSeconds)
    {
        _intervalSeconds = intervalSeconds;
    }

    public bool IsDue(float now) => _lastAt is not { } last || now - last >= _intervalSeconds || now < last;

    public void Ran(float now) => _lastAt = now;
}
