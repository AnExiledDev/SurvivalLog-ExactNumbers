using System;
using System.Threading.Tasks;

namespace ExactNumbers.Adapters;

/// <summary>
/// Runs the session's file writes (the measurements save and the trace batch) on a pool thread, one batch at a time, so
/// the game's frame never waits on the disk. The work must touch nothing of the game's: plain .NET only.
/// </summary>
internal sealed class FileWorker
{
    private Task _running = Task.CompletedTask;

    /// <summary>True while a batch is still being written; the next one waits for it.</summary>
    public bool IsBusy => !_running.IsCompleted;

    /// <summary>Starts work after the last batch; the caller checks IsBusy first. The work's own failures are swallowed.</summary>
    public void Start(Action work) =>
        _running = Task.Run(() =>
        {
            try
            {
                work();
            }
            catch (Exception)
            {
                // The save and the flush never throw; nothing on this thread may log to the game's console.
            }
        });

    /// <summary>Waits for the batch being written, at most timeout; for the game closing.</summary>
    /// <returns>False when it was still running at the timeout.</returns>
    public bool Finish(TimeSpan timeout)
    {
        try
        {
            return _running.Wait(timeout);
        }
        catch (Exception)
        {
            return true;
        }
    }
}
