using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// The labels as the page script takes them: an object keyed by preview text. Each value is a list of chips
/// ({"k": kind, "v": vanilla text, "s": shown text or null, "b": base text or null}), or null for a preview whose chips
/// are left as the game drew them.
/// </summary>
public static class PagePayload
{
    public static string Labels(IEnumerable<string> previews, Modifiers modifiers) =>
        "{" + string.Join(",", previews.Select(preview => JsText.Literal(preview) + ":" + Chips(ChipLabels.For(preview, modifiers)))) + "}";

    private static string Chips(IReadOnlyList<ChipLabel>? labels) =>
        labels == null ? "null" : "[" + string.Join(",", labels.Select(Chip)) + "]";

    private static string Chip(ChipLabel label) =>
        "{\"k\":" + JsText.Literal(ChipLabels.KindName(label.Kind)) +
        ",\"v\":" + JsText.Literal(label.Vanilla) +
        ",\"s\":" + Optional(label.Shown) +
        ",\"b\":" + Optional(label.Base) + "}";

    private static string Optional(string? text) => text == null ? "null" : JsText.Literal(text);
}
