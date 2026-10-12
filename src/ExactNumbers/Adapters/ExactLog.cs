using System;
using System.IO;
using System.Text;
using BepInEx.Logging;
using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts (LogOutput.log is overwritten on the
/// next launch). The file is kept under a size: past it, it is moved to exact-numbers.log.1 and started again.
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
            _bepinex.LogInfo(message);
        }
        catch (Exception)
        {
            // The file copy below still has a chance.
        }

        WriteFileOnly(message);
    }

    /// <summary>For the trace: far too many lines for the BepInEx console, so they go to the file alone. Never throws.</summary>
    public void WriteFileOnly(string message)
    {
        lock (_gate) AppendToFile($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
    }

    /// <summary>One writer stays open so a busy frame is not paying for an open and close per line.</summary>
    private void AppendToFile(string line)
    {
        try
        {
            _budget ??= new LogBudget(File.Exists(_path) ? new FileInfo(_path).Length : 0);

            if (_budget.Spend(Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length)) Rotate();

            _file ??= OpenFile();
            _file.WriteLine(line);
        }
        catch (Exception)
        {
            // The BepInEx log already has the line; the writer is dropped so the next line tries a fresh one.
            CloseFile();
        }
    }

    private void Rotate()
    {
        CloseFile();
        File.Move(_path, _path + ".1", true);
    }

    /// <summary>Shared for reading so the log can be tailed while the game runs; flushed per line so a crash loses nothing.</summary>
    private StreamWriter OpenFile() =>
        new(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };

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
