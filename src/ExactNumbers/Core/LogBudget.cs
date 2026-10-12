namespace ExactNumbers.Core;

/// <summary>
/// Keeps the log file under a size. When the next line would take the file past the cap, the file is moved aside (one
/// older copy is kept) and a fresh one started, so the log never grows past twice the cap however long the game runs.
/// </summary>
public sealed class LogBudget
{
    /// <summary>Enough for a few hours of tracing every stat change of the character.</summary>
    public const long DefaultMaxBytes = 10_000_000;

    private readonly long _maxBytes;
    private long _bytes;

    /// <param name="existingBytes">The size of the file the log goes on with.</param>
    public LogBudget(long existingBytes, long maxBytes = DefaultMaxBytes)
    {
        _maxBytes = maxBytes;
        _bytes = existingBytes < 0 ? 0 : existingBytes;
    }

    /// <returns>True when the file must be moved aside before this line is written. A line is never split or dropped.</returns>
    public bool Spend(long lineBytes)
    {
        if (_bytes > 0 && _bytes + lineBytes > _maxBytes)
        {
            _bytes = lineBytes;

            return true;
        }

        _bytes += lineBytes;

        return false;
    }
}
