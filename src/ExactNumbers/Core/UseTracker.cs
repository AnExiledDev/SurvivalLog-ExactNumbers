using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// The player's character starting an action. Target is the object it acts on, FuncId the furniture function the game
/// matched for it (0 for none), At the in-game clock in seconds (-1 when unknown). An action with a negative duration
/// runs until the player stops it (ActionComponent.PlayAction scales During by positive factors only, so the sign holds).
/// </summary>
public sealed record UseStart(int ActionId, long Target, int FuncId, bool IsIndefinite, int At);

/// <summary>
/// What one use measured for one stat, before the character's ratio: the total of a finite use, or the change per in-game
/// hour of an indefinite one. Reach is where the use's last change aimed (see CostCall.Reach), EndedAtMax whether the
/// stat ended at its maximum.
/// </summary>
public sealed record UseSample(MeasureKey Key, bool IsRate, double Amount, double Reach, bool EndedAtMax);

/// <summary>How a use ended, for the trace: what it measured, and why any stat it touched measured nothing.</summary>
public sealed record UseOutcome(UseStart Use, bool IsBroken, bool IsDiscarded, IReadOnlyList<UseSample> Samples, IReadOnlyList<string> Dropped);

/// <summary>
/// Adds up what one use of a furniture function does to each stat. A finite use counts only when it runs to its end: a
/// use the player broke off did part of its work. An indefinite one is measured as a rate over at least an in-game hour
/// and counts however it ends. A stat that went both up and down in one use is not measured, and neither is one the
/// character could not take in full during a finite use; in an indefinite use the rate stops at the first such change.
/// </summary>
public sealed class UseTracker
{
    public const int SecondsPerHour = 3600;
    public const int MinRateSeconds = SecondsPerHour;

    /// <summary>A use touches a handful of stats; past this a call is not tracked, so a runaway use cannot grow memory.</summary>
    public const int MaxKeys = 64;

    private readonly Dictionary<MeasureKey, Running> _running = new();
    private UseStart? _use;

    public UseStart? Current => _use;

    /// <returns>A use that was still open, which is thrown away: the game started something else before it ended.</returns>
    public UseStart? Start(UseStart use)
    {
        var open = _use;

        _use = use;
        _running.Clear();

        return open;
    }

    public TickVerdict Tick(CostCall call)
    {
        var verdict = TickRule.Judge(call);

        if (verdict is not (TickVerdict.Kept or TickVerdict.Clamped)) return verdict;
        if (_use is not { } use) return TickVerdict.NoUse;

        var func = call.FuncId > 0 ? call.FuncId : use.FuncId;

        if (func <= 0 || call.Stat is not { } stat) return TickVerdict.NoFunc;

        var key = new MeasureKey(new MeasureSite(call.ConfigId, func), stat);

        if (!_running.TryGetValue(key, out var running))
        {
            if (_running.Count >= MaxKeys) return TickVerdict.NoUse;

            running = new Running();
            _running[key] = running;
        }

        running.Add(call, verdict == TickVerdict.Clamped, use.IsIndefinite);

        return verdict;
    }

    /// <returns>Null when no use was open.</returns>
    public UseOutcome? End(bool isBroken)
    {
        if (_use is not { } use) return null;

        var running = _running.ToList();

        _use = null;
        _running.Clear();

        if (isBroken && !use.IsIndefinite) return new UseOutcome(use, true, true, Array.Empty<UseSample>(), running.Select(pair => Describe(pair.Key) + " broken off").ToList());

        var samples = new List<UseSample>();
        var dropped = new List<string>();

        foreach (var (key, total) in running)
        {
            var reason = total.Problem(use.IsIndefinite);

            if (reason != null) dropped.Add(Describe(key) + " " + reason);
            else samples.Add(total.Sample(key, use.IsIndefinite));
        }

        return new UseOutcome(use, isBroken, false, samples, dropped);
    }

    private static string Describe(MeasureKey key) => key.Site + " " + key.Stat;

    private sealed class Running
    {
        private double _sum;
        private double _sumAfterFirst;
        private int _ticks;
        private int _firstAt = -1;
        private int _lastAt = -1;
        private bool _hasGain;
        private bool _hasCost;
        private bool _isClamped;
        private bool _isStopped;
        private double _lastReach;
        private bool _lastAtMax;

        public void Add(CostCall call, bool isClamped, bool isIndefinite)
        {
            if (_isStopped) return;

            if (isClamped)
            {
                // A rate keeps what it measured before the stat hit its limit; a finite total is spoiled.
                if (isIndefinite) _isStopped = true;
                else _isClamped = true;

                return;
            }

            if (_ticks > 0) _sumAfterFirst += call.Value;
            else _firstAt = call.GameSeconds;

            _sum += call.Value;
            _ticks++;
            _lastAt = call.GameSeconds;
            _hasGain |= call.Value > 0;
            _hasCost |= call.Value < 0;
            _lastReach = call.Reach ?? 0;
            _lastAtMax = call.IsAtMax;
        }

        public string? Problem(bool isIndefinite)
        {
            if (_isClamped) return "hit a limit";
            if (_hasGain && _hasCost) return "went both up and down";
            if (_ticks == 0) return "no change counted";
            if (!isIndefinite) return null;
            if (_ticks < 2 || _firstAt < 0 || _lastAt < 0) return "too few changes for a rate";

            return _lastAt - _firstAt < MinRateSeconds ? "ran under an in-game hour" : null;
        }

        /// <summary>
        /// A rate counts the changes after the first over the time from the first to the last, so evenly spaced changes
        /// give the exact rate however long the use ran.
        /// </summary>
        public UseSample Sample(MeasureKey key, bool isIndefinite)
        {
            var amount = isIndefinite ? _sumAfterFirst / ((_lastAt - _firstAt) / (double)SecondsPerHour) : _sum;

            return new UseSample(key, isIndefinite, amount, _lastReach, _lastAtMax);
        }
    }
}
