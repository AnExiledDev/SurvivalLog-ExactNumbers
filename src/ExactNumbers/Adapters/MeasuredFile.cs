using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExactNumbers.Core;

namespace ExactNumbers.Adapters;

/// <summary>
/// BepInEx/exact-numbers-measured.json: the measurements, kept across sessions. Saved whole to a temporary file that
/// then replaces the old one, so a crash mid-save leaves the last good file. A file that cannot be read is moved to
/// exact-numbers-measured.json.bad and the mod starts with nothing measured; if it cannot be moved, nothing is saved
/// this session, so the file is left as it was.
/// </summary>
internal sealed class MeasuredFile
{
    private readonly string _path;
    private readonly string _version;
    private readonly Action<string> _log;
    private IReadOnlyList<OtherVersion> _others = Array.Empty<OtherVersion>();
    private bool _hasReportedSaveFailure;
    private bool _isSaveDisabled;

    public MeasuredFile(string path, string version, Action<string> log)
    {
        _path = path;
        _version = version;
        _log = log;
    }

    /// <summary>Never throws: without a readable file the mod measures from scratch.</summary>
    public MeasureStore Load()
    {
        try
        {
            if (!File.Exists(_path)) return Fresh("no measurements file yet");

            if (new FileInfo(_path).Length > MeasureFile.MaxBytes) return SetAside("it is larger than " + MeasureFile.MaxBytes + " bytes");

            var load = MeasureFile.Read(File.ReadAllText(_path, Encoding.UTF8), _version);

            if (load == null) return SetAside("it is not a measurements file this version can read");

            _others = load.Others;
            _log($"measurements loaded for game version {_version}: {load.Store.Count} records, {load.Skipped} unreadable records skipped, {load.Others.Count} other game versions kept");

            return load.Store;
        }
        catch (Exception ex)
        {
            // Set aside rather than left in place, so the next save cannot overwrite measurements that may still be there.
            return SetAside("reading it failed: " + ex.Message);
        }
    }

    /// <summary>Never throws; a failed save is reported once and tried again at the next save.</summary>
    public void Save(MeasureStore store)
    {
        if (_isSaveDisabled) return;

        var temporary = _path + ".tmp";

        try
        {
            File.WriteAllText(temporary, MeasureFile.Write(store, _version, _others), new UTF8Encoding(false));
            File.Move(temporary, _path, true);
            store.Saved();
        }
        catch (Exception ex)
        {
            if (!_hasReportedSaveFailure) _log("could not save the measurements (reported once): " + ex.Message);

            _hasReportedSaveFailure = true;
        }
    }

    private MeasureStore SetAside(string reason)
    {
        try
        {
            File.Move(_path, _path + ".bad", true);

            return Fresh($"measurements file set aside as {Path.GetFileName(_path)}.bad because {reason}; starting fresh");
        }
        catch (Exception ex)
        {
            // Saving would overwrite measurements that may still be in there.
            _isSaveDisabled = true;

            return Fresh($"measurements file unreadable because {reason}, and could not be set aside ({ex.Message}); measuring from scratch without saving this session");
        }
    }

    private MeasureStore Fresh(string reason)
    {
        _log(reason);

        return new MeasureStore();
    }
}
