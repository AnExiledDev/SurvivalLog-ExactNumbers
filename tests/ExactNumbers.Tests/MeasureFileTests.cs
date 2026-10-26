using ExactNumbers.Core;
using Xunit;
using static ExactNumbers.Tests.Uses;

namespace ExactNumbers.Tests;

public class MeasureFileTests
{
    private const string Version = "1.0.3";
    private const string EmptyVersion = "{\"seq\":0,\"records\":[]}";

    private static readonly MeasureKey BedStamina = new(Bed, Stat.Stamina);
    private static readonly MeasureKey ChairMorale = new(new MeasureSite(600, 40), Stat.Morale);

    private static string Record(string furniture, string stat) =>
        "{\"f\":" + furniture + ",\"u\":7,\"s\":\"" + stat + "\",\"k\":\"use\",\"n\":1,\"lo\":-5,\"hi\":-5,\"fill\":false,\"max\":false,\"seq\":1,\"recent\":[[-5,45]]}";

    [Fact]
    public void Measurements_written_for_a_game_version_read_back_the_same()
    {
        var store = Store(
            Sample(12.25, reach: 60),
            Sample(16, reach: 100, endedAtMax: true),
            Sample(4.5, isRate: true, site: ChairMorale.Site, stat: Stat.Morale));

        var load = MeasureFile.Read(MeasureFile.Write(store, Version, Array.Empty<OtherVersion>()), Version)!;

        foreach (var key in new[] { BedStamina, ChairMorale })
        {
            var written = store.Find(key)!;
            var read = load.Store.Find(key)!;

            Assert.Equal(written.Kind, read.Kind);
            Assert.Equal(written.Count, read.Count);
            Assert.Equal(written.Low, read.Low);
            Assert.Equal(written.High, read.High);
            Assert.Equal(written.IsFillTo, read.IsFillTo);
            Assert.Equal(written.HasEndedAtMax, read.HasEndedAtMax);
            Assert.Equal(written.Recent, read.Recent);
            Assert.Equal(written.Seq, read.Seq);
        }

        Assert.Equal(store.Seq, load.Store.Seq);
        Assert.Equal(0, load.Skipped);
        Assert.False(load.Store.IsDirty);
    }

    /// <summary>
    /// The file is written on another thread from a snapshot while the game goes on measuring; it must hold the store as
    /// it was when the snapshot was taken, and a later measurement waits for the next save.
    /// </summary>
    [Fact]
    public void A_snapshot_is_written_as_the_store_was_when_it_was_taken()
    {
        var store = Store(Sample(12.25, reach: 60));
        var snapshot = store.Snapshot();
        var before = MeasureFile.Write(store, Version, Array.Empty<OtherVersion>());

        store.Add(Sample(16, reach: 100, endedAtMax: true));
        store.Add(Sample(4.5, isRate: true, site: ChairMorale.Site, stat: Stat.Morale));

        var written = MeasureFile.Write(snapshot, Version, Array.Empty<OtherVersion>());
        var load = MeasureFile.Read(written, Version)!;

        Assert.Equal(before, written);
        Assert.Equal(1, load.Store.Find(BedStamina)!.Count);
        Assert.Null(load.Store.Find(ChairMorale));
        Assert.Equal(1, load.Store.Seq);
        Assert.NotEqual(written, MeasureFile.Write(store, Version, Array.Empty<OtherVersion>()));
    }

    /// <summary>A game update can change any function's numbers, so they start over, but going back finds the old ones.</summary>
    [Fact]
    public void Another_game_versions_measurements_are_kept_but_not_used()
    {
        var old = MeasureFile.Write(Store(Sample(-5)), "1.0.2", Array.Empty<OtherVersion>());

        var load = MeasureFile.Read(old, Version)!;

        Assert.Equal(0, load.Store.Count);
        Assert.Equal("1.0.2", Assert.Single(load.Others).Version);

        load.Store.Add(Sample(-7, site: ChairMorale.Site));

        var back = MeasureFile.Read(MeasureFile.Write(load.Store, Version, load.Others), "1.0.2")!;

        Assert.Equal(-5, back.Store.Find(BedStamina)!.Low);
        Assert.Null(back.Store.Find(new MeasureKey(ChairMorale.Site, Stat.Stamina)));
    }

    [Fact]
    public void Only_two_other_game_versions_are_kept()
    {
        var json = "{\"format\":1,\"versions\":{\"a\":" + EmptyVersion + ",\"b\":" + EmptyVersion + ",\"c\":" + EmptyVersion + "}}";

        var load = MeasureFile.Read(json, Version)!;

        Assert.Equal(MeasureFile.MaxOtherVersions, load.Others.Count);
        Assert.Equal(new[] { "a", "b" }, load.Others.Select(other => other.Version));
    }

    [Fact]
    public void A_file_with_nothing_for_this_game_version_starts_empty()
    {
        var load = MeasureFile.Read("{\"format\":1,\"versions\":{}}", Version);

        Assert.NotNull(load);
        Assert.Equal(0, load!.Store.Count);
    }

    /// <summary>The caller sets such a file aside and starts fresh, rather than overwriting what it cannot read.</summary>
    [Theory]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("{\"format\":2,\"versions\":{}}")]
    [InlineData("{\"format\":\"1\",\"versions\":{}}")]
    [InlineData("{\"format\":1}")]
    [InlineData("{\"format\":1,\"versions\":[]}")]
    public void A_file_that_is_not_a_measurements_file_this_mod_can_read_is_not_read(string json)
    {
        Assert.Null(MeasureFile.Read(json, Version));
    }

    /// <summary>
    /// Starting fresh would overwrite this version's measurements at the next save, so the file reads as unreadable and
    /// the caller sets it aside.
    /// </summary>
    [Theory]
    [InlineData("{\"seq\":1}")]
    [InlineData("[]")]
    [InlineData("{\"seq\":1,\"records\":{}}")]
    public void This_game_versions_measurements_in_a_shape_the_mod_cannot_read_make_the_file_unreadable(string entry)
    {
        Assert.Null(MeasureFile.Read("{\"format\":1,\"versions\":{\"" + Version + "\":" + entry + "}}", Version));
    }

    [Fact]
    public void Another_game_versions_entry_in_a_shape_the_mod_cannot_read_does_not_stop_this_versions_measurements()
    {
        var json = "{\"format\":1,\"versions\":{\"0.9\":[],\"" + Version + "\":{\"seq\":1,\"records\":[" + Record("500", "Stamina") + "]}}}";

        Assert.NotNull(MeasureFile.Read(json, Version)!.Store.Find(BedStamina));
    }

    [Fact]
    public void A_bad_record_is_skipped_and_the_rest_are_read()
    {
        var json = "{\"format\":1,\"versions\":{\"" + Version + "\":{\"seq\":1,\"records\":[" +
            Record("500", "Stamina") + "," + Record("0", "Satiety") + "," + Record("501", "Hunger") + "," + Record("\"502\"", "Morale") + ",7]}}}";

        var load = MeasureFile.Read(json, Version)!;

        Assert.Equal(1, load.Store.Count);
        Assert.Equal(4, load.Skipped);
        Assert.NotNull(load.Store.Find(BedStamina));
    }
}
