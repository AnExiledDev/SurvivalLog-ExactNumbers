using ExactNumbers.Core;
using Xunit;
using static ExactNumbers.Tests.Uses;

namespace ExactNumbers.Tests;

public class MeasureStoreTests
{
    private static readonly MeasureKey BedStamina = new(Bed, Stat.Stamina);

    private static MeasureRecord Sound() =>
        new(BedStamina, MeasureKind.PerUse, 2, -16, -12, false, false, new[] { new MeasuredAmount(-12, 38), new MeasuredAmount(-16, 34) }, 5);

    /// <summary>Float sums of the same fixed changes differ in the last digits; that is one amount, not a range.</summary>
    [Fact]
    public void The_same_amount_measured_again_stays_one_number()
    {
        var record = Store(Sample(-15), Sample(-15.01)).Find(BedStamina)!;

        Assert.Equal(2, record.Count);
        Assert.False(record.Varies);
    }

    [Fact]
    public void Different_amounts_make_a_range_from_the_smallest_to_the_largest()
    {
        var record = Store(Sample(12, reach: 40), Sample(16, reach: 70), Sample(14, reach: 55)).Find(BedStamina)!;

        Assert.Equal(12, record.Low);
        Assert.Equal(16, record.High);
        Assert.True(record.Varies);
        Assert.False(record.IsFillTo);
    }

    /// <summary>A function that tops a stat up to a limit gives less the fuller the stat was; no single number is right for it.</summary>
    [Fact]
    public void Amounts_that_vary_while_the_stat_ended_at_its_maximum_mark_a_fill_to_function()
    {
        var record = Store(Sample(12, reach: 60), Sample(16, reach: 100, endedAtMax: true)).Find(BedStamina)!;

        Assert.True(record.IsFillTo);
    }

    /// <summary>The limit can sit below the maximum, where only the reach gives it away.</summary>
    [Fact]
    public void Different_amounts_that_aimed_at_the_same_level_mark_a_fill_to_function()
    {
        var record = Store(Sample(12, reach: 80), Sample(16, reach: 80)).Find(BedStamina)!;

        Assert.True(record.IsFillTo);
    }

    [Fact]
    public void A_fixed_amount_that_ended_at_the_maximum_is_not_a_fill_to_function()
    {
        var record = Store(Sample(12, reach: 100, endedAtMax: true), Sample(12, reach: 100, endedAtMax: true)).Find(BedStamina)!;

        Assert.False(record.IsFillTo);
    }

    /// <summary>A game update can turn a finite action into one that runs until stopped; old totals are not rates.</summary>
    [Fact]
    public void A_sample_of_another_kind_starts_the_record_over()
    {
        var record = Store(Sample(12), Sample(12), Sample(4, isRate: true)).Find(BedStamina)!;

        Assert.Equal(MeasureKind.PerHour, record.Kind);
        Assert.Equal(1, record.Count);
        Assert.Equal(4, record.Low);
    }

    [Fact]
    public void An_amount_that_is_not_a_finite_number_is_not_kept()
    {
        var store = new MeasureStore();

        Assert.Null(store.Add(Sample(double.NaN)));
        Assert.Null(store.Add(Sample(5, reach: double.PositiveInfinity)));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Only_the_latest_amounts_are_kept_for_comparing()
    {
        var store = new MeasureStore();

        for (var i = 0; i < MeasureStore.MaxRecent + 4; i++) store.Add(Sample(10 + i));

        Assert.Equal(MeasureStore.MaxRecent, store.Find(BedStamina)!.Recent.Count);
        Assert.Equal(10 + MeasureStore.MaxRecent + 3, store.Find(BedStamina)!.Recent[^1].Amount);
    }

    [Fact]
    public void A_full_store_drops_the_record_that_changed_longest_ago()
    {
        var store = new MeasureStore();

        for (var i = 1; i <= MeasureStore.MaxRecords + 1; i++) store.Add(Sample(5, site: new MeasureSite(i, 1)));

        Assert.Equal(MeasureStore.MaxRecords, store.Count);
        Assert.Null(store.Find(new MeasureKey(new MeasureSite(1, 1), Stat.Stamina)));
        Assert.NotNull(store.Find(new MeasureKey(new MeasureSite(2, 1), Stat.Stamina)));
        Assert.NotNull(store.Find(new MeasureKey(new MeasureSite(MeasureStore.MaxRecords + 1, 1), Stat.Stamina)));
    }

    [Fact]
    public void A_record_read_back_that_breaks_the_stores_rules_is_skipped()
    {
        var store = new MeasureStore();

        Assert.False(store.Restore(Sound() with { Key = new MeasureKey(new MeasureSite(0, 7), Stat.Stamina) }));
        Assert.False(store.Restore(Sound() with { Low = -10 }));
        Assert.False(store.Restore(Sound() with { Count = 0 }));
        Assert.False(store.Restore(Sound() with { Recent = Array.Empty<MeasuredAmount>() }));
        Assert.False(store.Restore(Sound() with { High = double.NaN }));
        Assert.Equal(0, store.Count);
        Assert.True(store.Restore(Sound()));
        Assert.Equal(1, store.Count);
    }

    /// <summary>New samples must sort after everything read back, or the next save would drop a fresh record first.</summary>
    [Fact]
    public void Samples_after_a_load_are_numbered_after_every_record_read_back()
    {
        var store = new MeasureStore();

        store.Restore(Sound() with { Seq = 40 });

        Assert.Equal(41, store.Add(Sample(3, site: new MeasureSite(9, 9)))!.Seq);
    }

    [Fact]
    public void Only_a_new_measurement_needs_saving()
    {
        var store = new MeasureStore();

        store.Restore(Sound());

        Assert.False(store.IsDirty);

        store.Add(Sample(-14));

        Assert.True(store.IsDirty);

        store.Saved();

        Assert.False(store.IsDirty);
    }
}
