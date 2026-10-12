using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// What the page script is sent. Labels is an object keyed by preview text; each value is a list of chips
/// ({"k": kind, "v": vanilla text, "s": shown text or null, "t": hover text or null}), or null for a preview whose chips
/// are left as the game drew them. Game is the answer to one passed-on game message:
/// {"seq": its number, "menu": {"page": page id, "previews": [...]} or null, "labels": labels for its previews}.
/// </summary>
public static class PagePayload
{
    public static string Labels(IEnumerable<string> previews, Modifiers modifiers) =>
        "{" + string.Join(",", previews.Select(preview => JsText.Literal(preview) + ":" + Chips(ChipLabels.For(preview, modifiers)))) + "}";

    public static string Game(int seq, GameFacts facts, Modifiers modifiers) =>
        "{\"seq\":" + seq.ToString(CultureInfo.InvariantCulture) +
        ",\"menu\":" + Menu(facts.Menu) +
        ",\"labels\":" + Labels(facts.Previews, modifiers) + "}";

    private static string Menu(PageMenu? menu) =>
        menu == null ? "null" : "{\"page\":" + JsText.Literal(menu.PageId) + ",\"previews\":[" + string.Join(",", menu.Previews.Select(JsText.Literal)) + "]}";

    private static string Chips(IReadOnlyList<ChipLabel>? labels) =>
        labels == null ? "null" : "[" + string.Join(",", labels.Select(Chip)) + "]";

    private static string Chip(ChipLabel label) =>
        "{\"k\":" + JsText.Literal(ChipLabels.KindName(label.Kind)) +
        ",\"v\":" + JsText.Literal(label.Vanilla) +
        ",\"s\":" + Optional(label.Shown) +
        ",\"t\":" + Optional(label.Title) + "}";

    private static string Optional(string? text) => text == null ? "null" : JsText.Literal(text);
}
