using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// BepInEx/exact-numbers-measured.json: the measurements, kept across sessions. Saved whole to a temporary file that
/// then replaces the old one, so a crash mid-save leaves the last good file. A file that is not a measurements file is
/// moved to a dated exact-numbers-measured.json.&lt;date&gt;.bad and the mod starts with nothing measured. A file that
/// cannot be read right now (locked, or a folder in its place) is left as it is, and nothing is saved this session, so
/// it is still there next time. The same goes for one that cannot be moved.
/// </summary>
internal sealed class MeasuredFile
{
    private readonly string _path;
    private readonly string _version;
    private readonly Action<string> _log;
    private IReadOnlyList<OtherVersion> _others = Array.Empty<OtherVersion>();
    private bool _hasReportedSaveFailure;
    private bool _isSaveDisabled;
    private string? _failure;

    public MeasuredFile(string path, string version, Action<string> log)
    {
        _path = path;
        _version = version;
        _log = log;
    }

    /// <summary>Never throws: without a readable file the mod measures from scratch.</summary>
    public MeasureStore Load()
    {
        var text = ReadText();

        if (text == null) return new MeasureStore();

        var load = Parse(text);

        if (load == null) return SetAside("it is not a measurements file this version can read");

        _others = load.Others;
        _log($"measurements loaded for game version {_version}: {load.Store.Count} records, {load.Skipped} unreadable records skipped, {load.Others.Count} other game versions kept");

        return load.Store;
    }

    /// <summary>Null when there is nothing to read, logged; a file too large to be one is set aside first.</summary>
    private string? ReadText()
    {
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > MeasureFile.MaxBytes)
            {
                SetAside("it is larger than " + MeasureFile.MaxBytes + " bytes");

                return null;
            }

            return File.ReadAllText(_path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Fresh("no measurements file yet");

            return null;
        }
        catch (Exception ex)
        {
            // Locked by another program, or not a file: the measurements may still be in there, so nothing is saved over it.
            _isSaveDisabled = true;
            _log($"could not read the measurements file ({ex.Message}); measuring from scratch without saving this session, and the file is left as it is");

            return null;
        }
    }

    private MeasureLoad? Parse(string text)
    {
        try
        {
            return MeasureFile.Read(text, _version);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the snapshot and marks it saved in the store. Runs on the file worker, so it does not log: a failure is
    /// kept for ReportFailure and tried again at the next save. Never throws.
    /// </summary>
    public void Save(MeasureSnapshot snapshot, MeasureStore store)
    {
        if (_isSaveDisabled) return;

        var temporary = _path + ".tmp";

        try
        {
            File.WriteAllText(temporary, MeasureFile.Write(snapshot, _version, _others), new UTF8Encoding(false));
            File.Move(temporary, _path, true);
            store.Saved(snapshot);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _failure, ex.Message);
        }
    }

    /// <summary>On the main thread: logs the first failed save of the session. Never throws.</summary>
    public void ReportFailure()
    {
        var failure = Interlocked.Exchange(ref _failure, null);

        if (failure == null || _hasReportedSaveFailure) return;

        _hasReportedSaveFailure = true;
        _log("could not save the measurements (reported once): " + failure);
    }

    private MeasureStore SetAside(string reason)
    {
        try
        {
            var badPath = FreeBadPath();

            File.Move(_path, badPath, false);

            return Fresh($"measurements file set aside as {Path.GetFileName(badPath)} because {reason}; starting fresh");
        }
        catch (Exception ex)
        {
            // Saving would overwrite measurements that may still be in there.
            _isSaveDisabled = true;

            return Fresh($"measurements file unreadable because {reason}, and could not be set aside ({ex.Message}); measuring from scratch without saving this session");
        }
    }

    /// <summary>A dated name, with -2, -3 and so on after it when that one is taken, so an older copy is never replaced.</summary>
    private string FreeBadPath()
    {
        var stem = _path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var candidate = stem + ".bad";

        for (var n = 2; File.Exists(candidate) || Directory.Exists(candidate); n++) candidate = stem + "-" + n + ".bad";

        return candidate;
    }

    private MeasureStore Fresh(string reason)
    {
        _log(reason);

        return new MeasureStore();
    }
}
