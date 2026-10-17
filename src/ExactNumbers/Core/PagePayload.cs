using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// What the page script is sent. Labels is an object keyed by label key (a plain preview, or "@furniture/func/preview",
/// see LabelKey); each value is a list of chips ({"k": kind, "v": vanilla text, "s": shown text or null, "t": hover text
/// or null}), or null for a key whose chips are left as the game drew them. Game is the answer to one passed-on game
/// message: {"seq": its number, "menu": {"page": page id, "keys": [...]} or null, "recipes": {recipe key: label key},
/// "labels": labels}.
/// </summary>
public static class PagePayload
{
    /// <param name="isChinese">Whether the chips' words and hover text are written in Simplified Chinese.</param>
    public static string Labels(IEnumerable<string> keys, Modifiers modifiers, MeasureStore? measured, bool isChinese) =>
        "{" + string.Join(",", keys.Select(key => JsText.Literal(key) + ":" + Chips(LabelsFor(key, modifiers, measured, isChinese)))) + "}";

    /// <param name="labelKeys">The keys whose labels go with the answer; the page already holds the others.</param>
    public static string Game(int seq, GameAnswer answer, IEnumerable<string> labelKeys, Modifiers modifiers, MeasureStore? measured, bool isChinese) =>
        "{\"seq\":" + seq.ToString(CultureInfo.InvariantCulture) +
        ",\"menu\":" + Menu(answer.Menu) +
        ",\"recipes\":" + Recipes(answer.Recipes) +
        ",\"labels\":" + Labels(labelKeys, modifiers, measured, isChinese) + "}";

    private static IReadOnlyList<ChipLabel>? LabelsFor(string key, Modifiers modifiers, MeasureStore? measured, bool isChinese)
    {
        var (site, preview) = LabelKey.Parse(key);

        return ChipLabels.For(preview, modifiers, site, measured, isChinese);
    }

    private static string Menu(KeyedMenu? menu) =>
        menu == null ? "null" : "{\"page\":" + JsText.Literal(menu.PageId) + ",\"keys\":[" + string.Join(",", menu.Keys.Select(JsText.Literal)) + "]}";

    private static string Recipes(IReadOnlyDictionary<string, string> recipes) =>
        "{" + string.Join(",", recipes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => JsText.Literal(pair.Key) + ":" + JsText.Literal(pair.Value))) + "}";

    private static string Chips(IReadOnlyList<ChipLabel>? labels) =>
        labels == null ? "null" : "[" + string.Join(",", labels.Select(Chip)) + "]";

    private static string Chip(ChipLabel label) =>
        "{\"k\":" + JsText.Literal(ChipLabels.KindName(label.Kind)) +
        ",\"v\":" + JsText.Literal(label.Vanilla) +
        ",\"s\":" + Optional(label.Shown) +
        ",\"t\":" + Optional(label.Title) + "}";

    private static string Optional(string? text) => text == null ? "null" : JsText.Literal(text);
}
