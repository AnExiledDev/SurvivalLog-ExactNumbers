using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>What a measurement counts: the total of one completed use, or the change per in-game hour.</summary>
public enum MeasureKind
{
    PerUse,
    PerHour,
}

/// <summary>One measured use, kept so a later one can be compared with it: its amount and where it aimed.</summary>
public readonly record struct MeasuredAmount(double Amount, double Reach);

/// <summary>
/// Everything measured for one stat of one furniture function, before the character's ratio. Low and High are the
/// smallest and largest amounts seen. IsFillTo marks a function that tops a stat up to a limit instead of adding a fixed
/// amount, so its amount depends on where the stat started and no single number is right for it; one use of an effect
/// that fills to a limit marks it, even when the amounts seen so far agree. Seq orders records by
/// last change, so the oldest can be dropped.
/// </summary>
public sealed record MeasureRecord(
    MeasureKey Key,
    MeasureKind Kind,
    int Count,
    double Low,
    double High,
    bool IsFillTo,
    bool HasEndedAtMax,
    IReadOnlyList<MeasuredAmount> Recent,
    long Seq)
{
    public bool Varies => !MeasureMath.Same(Low, High);
}

public static class MeasureMath
{
    /// <summary>
    /// Two amounts the chips would show alike: within 0.05, or within 1% of the larger. Float sums of the same fixed
    /// changes differ in the last digits, which is not a range.
    /// </summary>
    public static bool Same(double a, double b) => Math.Abs(a - b) <= Math.Max(0.05, 0.01 * Math.Max(Math.Abs(a), Math.Abs(b)));

    /// <summary>Two reaches the game's fill-to rule would give for the same limit.</summary>
    public static bool SameReach(double a, double b) => Math.Abs(a - b) <= 0.01;
}

/// <summary>
/// The measured numbers, bounded. A sample of a different kind than its record (a function that changed from finite to
/// indefinite in a game update) starts the record over.
/// </summary>
public sealed class MeasureStore
{
    /// <summary>Far above the furniture functions in the game times four stats; past it the least recently changed record goes.</summary>
    public const int MaxRecords = 2000;

    public const int MaxRecent = 8;

    /// <summary>Counts stop here; a number seen this often is not going to change.</summary>
    public const int MaxCount = 1_000_000;

    private readonly Dictionary<MeasureKey, MeasureRecord> _records = new();
    private long _seq;

    public MeasureStore(long seq = 0)
    {
        _seq = Math.Max(0, seq);
    }

    public int Count => _records.Count;

    public long Seq => _seq;

    /// <summary>True when something changed since the last save.</summary>
    public bool IsDirty { get; private set; }

    public IEnumerable<MeasureRecord> Records => _records.Values.OrderBy(record => record.Seq);

    public MeasureRecord? Find(MeasureKey key) => _records.TryGetValue(key, out var record) ? record : null;

    public void Saved() => IsDirty = false;

    /// <returns>The record after the sample, or null for an amount that is not a finite number.</returns>
    public MeasureRecord? Add(UseSample sample)
    {
        if (!double.IsFinite(sample.Amount) || !double.IsFinite(sample.Reach)) return null;

        var kind = sample.IsRate ? MeasureKind.PerHour : MeasureKind.PerUse;
        var seen = new MeasuredAmount(sample.Amount, sample.Reach);
        var old = Find(sample.Key);
        var record = old == null || old.Kind != kind
            ? new MeasureRecord(sample.Key, kind, 1, sample.Amount, sample.Amount, sample.IsFillTo, sample.EndedAtMax, new[] { seen }, ++_seq)
            : Grow(old, sample, seen);

        Put(record);

        return record;
    }

    /// <summary>A record read back from the file. One that breaks the store's rules is skipped and reported false.</summary>
    public bool Restore(MeasureRecord record)
    {
        if (!IsSound(record)) return false;

        Put(record with { Recent = record.Recent.TakeLast(MaxRecent).ToList() });

        _seq = Math.Max(_seq, record.Seq);
        IsDirty = false;

        return true;
    }

    private MeasureRecord Grow(MeasureRecord old, UseSample sample, MeasuredAmount seen)
    {
        var recent = old.Recent.Append(seen).TakeLast(MaxRecent).ToList();
        var grown = old with
        {
            Count = Math.Min(MaxCount, old.Count + 1),
            Low = Math.Min(old.Low, sample.Amount),
            High = Math.Max(old.High, sample.Amount),
            HasEndedAtMax = old.HasEndedAtMax || sample.EndedAtMax,
            Recent = recent,
            Seq = ++_seq,
        };

        return grown with { IsFillTo = old.IsFillTo || sample.IsFillTo || IsFillTo(grown) };
    }

    /// <summary>
    /// For a fill-to effect the hooks did not see: amounts that vary while the stat ended at its maximum, or two
    /// different amounts that aimed at the same level, mean the function fills the stat up to a limit
    /// (AE_AddCostAttr_Float.c:71-79) rather than adding a set amount.
    /// </summary>
    private static bool IsFillTo(MeasureRecord record)
    {
        if (!record.Varies) return false;
        if (record.HasEndedAtMax) return true;

        var recent = record.Recent;

        for (var i = 0; i < recent.Count; i++)
        {
            for (var j = i + 1; j < recent.Count; j++)
            {
                if (!MeasureMath.Same(recent[i].Amount, recent[j].Amount) && MeasureMath.SameReach(recent[i].Reach, recent[j].Reach)) return true;
            }
        }

        return false;
    }

    private static bool IsSound(MeasureRecord record) =>
        record.Key.Site.IsKnown &&
        Enum.IsDefined(record.Kind) &&
        Enum.IsDefined(record.Key.Stat) &&
        record.Count is > 0 and <= MaxCount &&
        double.IsFinite(record.Low) && double.IsFinite(record.High) && record.Low <= record.High &&
        record.Seq > 0 &&
        record.Recent.Count > 0 &&
        record.Recent.All(seen => double.IsFinite(seen.Amount) && double.IsFinite(seen.Reach));

    private void Put(MeasureRecord record)
    {
        _records[record.Key] = record;
        IsDirty = true;

        if (_records.Count <= MaxRecords) return;

        var oldest = _records.Values.Where(other => other.Key != record.Key).MinBy(other => other.Seq);

        if (oldest != null) _records.Remove(oldest.Key);
    }
}
