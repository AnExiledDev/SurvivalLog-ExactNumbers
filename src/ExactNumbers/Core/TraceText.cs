using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// The lines of the trace log. Everything the hooks and the menu reader saw goes in, measured or not, so questions
/// about the game can be answered from a player's log later. One event per line, "name=value" pairs, invariant numbers.
/// </summary>
public static class TraceText
{
    private const string None = "none";

    public static string Cost(CostCall call, TickVerdict verdict) =>
        "cost attr=" + call.Attr +
        " value=" + Number(call.Value) +
        " requested=" + Number(call.Change?.Requested) +
        " applied=" + Number(call.Change?.Applied) +
        " before=" + Number(call.Change?.Before) +
        " after=" + Number(call.Change?.After) +
        " max=" + Number(call.Max) +
        " reach=" + Number(call.Reach) +
        " source=" + call.Source +
        " config=" + Whole(call.ConfigId) +
        " target=" + Whole(call.Target) +
        " args=" + call.ArgsType +
        " dep=" + Flag(call.IsDependency) +
        " action=" + Whole(call.ActionId) +
        " func=" + Whole(call.FuncId) +
        " time=" + Whole(call.GameSeconds) +
        " fill=" + Flag(call.IsFillTo) +
        " -> " + verdict;

    public static string Start(UseStart use) => "use start " + Use(use);

    public static string Continued(UseStart use) => "use start, same press as the last action: " + Use(use);

    public static string RewardOpened(int actionId, MeasureSite site) => "reward start action=" + Whole(actionId) + " site=" + site;

    public const string RewardClosed = "reward end";

    public static string Dropped(int count) =>
        "trace: " + Whole(count) + " lines dropped, more than " + Whole(TraceBuffer.MaxLines) + " came between two saves";

    public static string Discarded(UseStart use) => "use discarded, another action started before it ended: " + Use(use);

    public static string End(UseOutcome outcome, int at)
    {
        var result = outcome.IsHeld ? "held for the press's next action" : outcome.IsDiscarded ? "discarded" : "kept " + outcome.Samples.Count;
        var dropped = outcome.Dropped.Count == 0 ? "" : "; dropped: " + string.Join(", ", outcome.Dropped);

        return "use end " + Use(outcome.Use) + " broken=" + Flag(outcome.IsBroken) + " end=" + Whole(at) + " -> " + result + dropped;
    }

    public static string EndIgnored(int actionId, int openActionId) =>
        "use end ignored: action " + Whole(actionId) + " ended while action " + Whole(openActionId) + " is the open use";

    public static string EndWithoutUse(int actionId) => "use end with no open use: action " + Whole(actionId);

    public static string Measured(MeasureRecord record) =>
        "measured " + record.Key.Site + " " + record.Key.Stat + ": " +
        (record.Kind == MeasureKind.PerHour ? "per hour" : "per use") +
        " n=" + Whole(record.Count) +
        " lo=" + Number(record.Low) +
        " hi=" + Number(record.High) +
        " fill=" + Flag(record.IsFillTo) +
        " endedAtMax=" + Flag(record.HasEndedAtMax) +
        " recent=" + string.Join(" ", record.Recent.Select(seen => Number(seen.Amount) + "@" + Number(seen.Reach)));

    /// <summary>What the menu path read for each button, so a log shows whether the message carries func ids at all.</summary>
    public static string Menu(string pageId, MenuState? state, IReadOnlyList<MenuRead> reads)
    {
        var furniture = state == null ? "no state" : Whole(state.ConfigId) + " state buttons=" + Whole(state.Buttons.Count);
        var buttons = reads.Select(read => Whole(read.Index) + " json=" + Optional(read.JsonFunc) + " state=" + Optional(read.StateFunc) + " key=" + read.Key);

        return "menu page=" + pageId + " furniture=" + furniture + " buttons: " + string.Join(" | ", buttons);
    }

    public static string Recipes(int products, IReadOnlyDictionary<string, string> mapped) =>
        "recipes answered: " + Whole(products) + " products, " + Whole(mapped.Count) + " with a known function" +
        string.Concat(mapped.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => " | " + pair.Key + " -> " + pair.Value));

    private static string Use(UseStart use) =>
        "action=" + Whole(use.ActionId) +
        " target=" + Whole(use.Target) +
        " func=" + Whole(use.FuncId) +
        " indefinite=" + Flag(use.IsIndefinite) +
        " press=" + Whole(use.Press) +
        " time=" + Whole(use.At);

    private static string Number(double? value) => value is { } known ? known.ToString("0.####", CultureInfo.InvariantCulture) : None;

    private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Optional(int? value) => value is { } known ? Whole(known) : "absent";

    private static string Flag(bool value) => value ? "yes" : "no";
}
