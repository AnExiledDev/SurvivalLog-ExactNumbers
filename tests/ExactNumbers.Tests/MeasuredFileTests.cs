using ExactNumbers.Adapters;
using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

/// <summary>The measurements file on a real folder: what a bad or unreadable file does to the measurements in it.</summary>
public sealed class MeasuredFileTests : IDisposable
{
    private const string Version = "1.1.21671";

    private readonly string _folder = Directory.CreateTempSubdirectory("exact-numbers-").FullName;
    private readonly List<string> _log = new();

    private string FilePath => Path.Combine(_folder, "exact-numbers-measured.json");

    private MeasuredFile File() => new(FilePath, Version, _log.Add);

    public void Dispose() => Directory.Delete(_folder, true);

    private static MeasureStore Sample() => Uses.Store(Uses.Sample(-10, site: new MeasureSite(500, 7), stat: Stat.Satiety));

    private string[] SetAside() => Directory.GetFiles(_folder, "*.bad").Select(Path.GetFileName).OfType<string>().ToArray();

    [Fact]
    public void Without_a_file_it_starts_fresh_and_saves()
    {
        var file = File();
        var store = file.Load();
        var sample = Sample();

        file.Save(sample.Snapshot(), sample);

        Assert.Equal(0, store.Count);
        Assert.True(System.IO.File.Exists(FilePath));
        Assert.Equal(1, File().Load().Count);
    }

    [Fact]
    public void Two_bad_files_are_both_kept()
    {
        System.IO.File.WriteAllText(FilePath, "first bad file");
        File().Load();
        System.IO.File.WriteAllText(FilePath, "second bad file");
        File().Load();

        var kept = SetAside().Select(name => System.IO.File.ReadAllText(Path.Combine(_folder, name))).OrderBy(text => text).ToArray();

        Assert.Equal(new[] { "first bad file", "second bad file" }, kept);
        Assert.False(System.IO.File.Exists(FilePath));
    }

    /// <summary>A file the mod cannot read right now may still hold every measurement, so it is never saved over.</summary>
    [Fact]
    public void A_file_that_cannot_be_read_is_left_alone_and_never_saved_over()
    {
        Directory.CreateDirectory(FilePath);

        var file = File();
        var sample = Sample();

        Assert.Equal(0, file.Load().Count);

        file.Save(sample.Snapshot(), sample);

        Assert.True(Directory.Exists(FilePath));
        Assert.Empty(SetAside());
        Assert.False(System.IO.File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void A_file_too_large_to_be_one_is_set_aside()
    {
        System.IO.File.WriteAllText(FilePath, new string(' ', (int)MeasureFile.MaxBytes + 1));

        Assert.Equal(0, File().Load().Count);
        Assert.Single(SetAside());
        Assert.False(System.IO.File.Exists(FilePath));
    }
}
