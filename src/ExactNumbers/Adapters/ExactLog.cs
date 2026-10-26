using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Logging;
using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts (LogOutput.log is overwritten on the
/// next launch). The file is kept under a size: past it, it is moved to exact-numbers.log.1 and started again. If it
/// cannot be moved, lines are dropped, and after a few failed moves nothing more is written to it this session.
/// </summary>
internal sealed class ExactLog
{
    private readonly ManualLogSource _bepinex;
    private readonly string _path;
    private readonly object _gate = new();
    private LogBudget? _budget;
    private StreamWriter? _file;

    public ExactLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _path = path;
    }

    /// <summary>Never throws: a logger that can fail would take down whatever it was asked to report on.</summary>
    public void Write(string message)
    {
        try
        {
            if (HookFailure.Is(message)) _bepinex.LogError(message);
            else _bepinex.LogInfo(message);
        }
        catch (Exception)
        {
            // The file copy below still has a chance.
        }

        WriteFileOnly(message);
    }

    /// <summary>For lines too many for the BepInEx console: they go to the file alone. Never throws.</summary>
    public void WriteFileOnly(string message) => WriteStamped(new[] { Stamp(message) });

    /// <summary>Lines already stamped (Stamp), written in order and flushed once. Never throws.</summary>
    public void WriteStamped(IReadOnlyList<string> lines)
    {
        lock (_gate)
        {
            foreach (var line in lines) AppendToFile(line);

            FlushFile();
        }
    }

    /// <summary>The time a line was logged, so a line written later in a batch still says when it happened.</summary>
    public static string Stamp(string message) => Stamp(DateTime.Now, message);

    /// <summary>The same for a line that happened at a local time before it was written.</summary>
    public static string Stamp(DateTime at, string message) => $"{at:yyyy-MM-dd HH:mm:ss.fff} {message}";

    /// <summary>One writer stays open so a busy frame is not paying for an open and close per line.</summary>
    private void AppendToFile(string line)
    {
        try
        {
            _budget ??= new LogBudget(File.Exists(_path) ? new FileInfo(_path).Length : 0);

            if (_budget.IsStopped) return;

            var lineBytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            if (_budget.IsFull(lineBytes) && !TryRotate(_budget)) return;

            _file ??= OpenFile();
            _file.WriteLine(line);
            _budget.Spend(lineBytes);
        }
        catch (Exception)
        {
            // The BepInEx log already has the line; the writer is dropped so the next line tries a fresh one.
            CloseFile();
        }
    }

    /// <returns>False when the file could not be moved aside; the caller drops its line rather than grow the file.</returns>
    private bool TryRotate(LogBudget budget)
    {
        CloseFile();

        try
        {
            if (File.Exists(_path)) File.Move(_path, _path + ".1", true);
        }
        catch (Exception ex)
        {
            budget.RotationFailed();

            if (budget.IsStopped) ReportStopped(ex);

            return false;
        }

        budget.Rotated();

        return true;
    }

    private void ReportStopped(Exception ex)
    {
        try
        {
            _bepinex.LogWarning($"{Path.GetFileName(_path)} is full and could not be moved aside {LogBudget.MaxFailedRotations} times in a row ({ex.Message}); nothing more is written to it this session");
        }
        catch (Exception)
        {
            // Nowhere left to say it.
        }
    }

    /// <summary>Shared for reading so the log can be tailed while the game runs; flushed after each batch, not each line.</summary>
    private StreamWriter OpenFile() =>
        new(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = false };

    private void FlushFile()
    {
        try
        {
            _file?.Flush();
        }
        catch (Exception)
        {
            CloseFile();
        }
    }

    private void CloseFile()
    {
        try
        {
            _file?.Dispose();
        }
        catch (Exception)
        {
            // A writer that failed to write can fail to close too; it is being thrown away either way.
        }

        _file = null;
    }
}
