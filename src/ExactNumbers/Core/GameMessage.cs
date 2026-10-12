using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ExactNumbers.Core;

/// <summary>An object menu the game sent to one menu page: each button's preview text in button order, "" for a button without one.</summary>
public sealed record PageMenu(string PageId, IReadOnlyList<string> Previews);

/// <summary>What one game-to-page message tells the mod: the object menu it carries, if it is one, and every preview text in it.</summary>
public sealed record GameFacts(PageMenu? Menu, IReadOnlyList<string> Previews)
{
    public static readonly GameFacts Nothing = new(null, Array.Empty<string>());
}

/// <summary>
/// Reads the messages the game sends its web pages, in the form Root.html receives them (Root.html:252-262):
/// "type\x1Epage id\x1Emessage name\x1Ejson". Type 8 is a message for one page; Root hands its json to the iframe whose
/// id is the page id (Root.html:291-299, ids set at 493-494).
/// </summary>
public static class GameMessage
{
    /// <summary>Far above any page message the game sends; a longer one is not read.</summary>
    public const int MaxLength = 1_000_000;

    /// <summary>Far above the buttons of any menu or the recipes of any table; past this a message's previews are not collected.</summary>
    public const int MaxPreviews = 256;

    public const string MenuMessage = "WebUI_SelectMenu_MenuMsg";

    private const char Separator = '\u001e';
    private const string PageMessageType = "8";

    /// <summary>SelectMenu.html reads btnList[i].attrDelta (line 537); ToolTable.html reads productAttrDelta (line 3433).</summary>
    private static readonly HashSet<string> PreviewFields = new(StringComparer.Ordinal) { "attrDelta", "productAttrDelta" };

    public static GameFacts Read(string? raw)
    {
        if (raw is null || raw.Length > MaxLength) return GameFacts.Nothing;

        var parts = raw.Split(Separator, 4);

        if (parts.Length < 4 || parts[0] != PageMessageType) return GameFacts.Nothing;

        try
        {
            using var document = JsonDocument.Parse(parts[3]);

            var data = document.RootElement;
            var menu = parts[2] == MenuMessage ? MenuOf(parts[1], data) : null;
            var previews = new List<string>();

            CollectPreviews(data, previews);

            return new GameFacts(menu, previews);
        }
        catch (JsonException)
        {
            return GameFacts.Nothing;
        }
    }

    /// <summary>Null when the message has no button list: the page keeps its old list then (SelectMenu.html:439-443), and so does the mod.</summary>
    private static PageMenu? MenuOf(string pageId, JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("btnList", out var buttons) || buttons.ValueKind != JsonValueKind.Array) return null;

        var previews = new List<string>();

        foreach (var button in buttons.EnumerateArray()) previews.Add(PreviewOf(button));

        return new PageMenu(pageId, previews);
    }

    private static string PreviewOf(JsonElement button)
    {
        if (button.ValueKind != JsonValueKind.Object || !button.TryGetProperty("attrDelta", out var preview)) return "";

        return preview.ValueKind == JsonValueKind.String ? preview.GetString() ?? "" : "";
    }

    /// <summary>Every preview in the message, so its numbers can be on the page before the first hover. JsonDocument caps the depth.</summary>
    private static void CollectPreviews(JsonElement element, List<string> previews)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (PreviewFields.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.String) Add(property.Value.GetString(), previews);
                    else CollectPreviews(property.Value, previews);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectPreviews(item, previews);

                break;
        }
    }

    private static void Add(string? preview, List<string> previews)
    {
        if (string.IsNullOrWhiteSpace(preview) || preview.Length > PageCommand.MaxPreviewLength) return;
        if (previews.Count >= MaxPreviews || previews.Contains(preview)) return;

        previews.Add(preview);
    }
}
