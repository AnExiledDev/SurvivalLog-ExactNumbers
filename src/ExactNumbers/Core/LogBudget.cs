namespace ExactNumbers.Core;

/// <summary>
/// Keeps the log file under a size. When the next line would take the file past the cap, the file is moved aside (one
/// older copy is kept) and a fresh one started, so the log never grows past twice the cap however long the game runs.
/// A file that cannot be moved aside stays full: lines are dropped rather than written past the cap, and after a few
/// failed moves in a row the log stops.
/// </summary>
public sealed class LogBudget
{
    /// <summary>Enough for a few hours of tracing every stat change of the character.</summary>
    public const long DefaultMaxBytes = 10_000_000;

    /// <summary>Failed moves in a row before the log stops; a move that fails this often will not start working.</summary>
    public const int MaxFailedRotations = 3;

    private readonly long _maxBytes;
    private long _bytes;
    private int _failedRotations;

    /// <param name="existingBytes">The size of the file the log goes on with.</param>
    public LogBudget(long existingBytes, long maxBytes = DefaultMaxBytes)
    {
        _maxBytes = maxBytes;
        _bytes = existingBytes < 0 ? 0 : existingBytes;
    }

    /// <summary>True once moving the file aside has failed MaxFailedRotations times in a row: nothing more is written.</summary>
    public bool IsStopped => _failedRotations >= MaxFailedRotations;

    /// <summary>
    /// True when the file must be moved aside before a line of lineBytes is written. An empty file never is: moving it
    /// gains nothing, so a line longer than the cap still goes in whole.
    /// </summary>
    public bool IsFull(long lineBytes) => _bytes > 0 && _bytes + lineBytes > _maxBytes;

    /// <summary>A line of lineBytes was written.</summary>
    public void Spend(long lineBytes) => _bytes += lineBytes;

    /// <summary>The file was moved aside; the log starts again empty.</summary>
    public void Rotated()
    {
        _bytes = 0;
        _failedRotations = 0;
    }

    /// <summary>Moving the file aside failed. It is still full, so the line that asked is dropped.</summary>
    public void RotationFailed() => _failedRotations++;
}
