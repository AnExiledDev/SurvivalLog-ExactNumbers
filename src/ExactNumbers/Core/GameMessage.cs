using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ExactNumbers.Core;

/// <summary>
/// An object menu the game sent to one menu page: each button's preview text in button order, "" for a button without
/// one, and each button's furniture function id when the message carries one (null when it does not; SelectMenu.html
/// never reads one, so the plugin also reads the menu's state on the C# side).
/// </summary>
public sealed record PageMenu(string PageId, IReadOnlyList<string> Previews, IReadOnlyList<int?> FuncIds);

/// <summary>A crafting recipe's product preview, with the recipe key the page knows the recipe by (ToolTable.html:3271, 3420-3434).</summary>
public sealed record RecipeProduct(string RecipeKey, string Preview);

/// <summary>
/// What one game-to-page message tells the mod: the object menu it carries, if it is one, every preview text in it, and
/// every recipe product preview with its recipe key.
/// </summary>
public sealed record GameFacts(PageMenu? Menu, IReadOnlyList<string> Previews, IReadOnlyList<RecipeProduct> Products)
{
    public static readonly GameFacts Nothing = new(null, Array.Empty<string>(), Array.Empty<RecipeProduct>());
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

    /// <summary>Recipe keys are short ids; a longer one is not mapped.</summary>
    public const int MaxRecipeKeyLength = 200;

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

        var isMenu = parts[2] == MenuMessage;

        // Besides bad JSON: Parse throws ArgumentException on a lone surrogate in the text, and GetString throws
        // InvalidOperationException on one written as an escape.
        try
        {
            using var document = JsonDocument.Parse(parts[3]);

            var data = document.RootElement;
            var menu = isMenu ? MenuOf(parts[1], data) : null;
            var previews = new List<string>();
            var products = new List<RecipeProduct>();

            CollectPreviews(data, previews);
            CollectProducts(data, products);

            return new GameFacts(menu, previews, products);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            return isMenu ? UnreadableMenu(parts[1]) : GameFacts.Nothing;
        }
    }

    /// <summary>
    /// A menu whose buttons cannot be read has no previews, so the page drops the ones it holds for that page instead of
    /// putting the previous open's numbers on the new menu's chips.
    /// </summary>
    private static GameFacts UnreadableMenu(string pageId) =>
        new(new PageMenu(pageId, Array.Empty<string>(), Array.Empty<int?>()), Array.Empty<string>(), Array.Empty<RecipeProduct>());

    /// <summary>Null when the message has no button list: the page keeps its old list then (SelectMenu.html:439-443), and so does the mod.</summary>
    private static PageMenu? MenuOf(string pageId, JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("btnList", out var buttons) || buttons.ValueKind != JsonValueKind.Array) return null;

        var previews = new List<string>();
        var funcIds = new List<int?>();

        foreach (var button in buttons.EnumerateArray())
        {
            previews.Add(PreviewOf(button));
            funcIds.Add(FuncIdOf(button));
        }

        return new PageMenu(pageId, previews, funcIds);
    }

    /// <summary>A button's "funcId", when the message carries a positive whole number there.</summary>
    private static int? FuncIdOf(JsonElement button)
    {
        if (button.ValueKind != JsonValueKind.Object || !button.TryGetProperty("funcId", out var id)) return null;

        return id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var funcId) && funcId > 0 ? funcId : null;
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

    /// <summary>
    /// Every object holding both a product preview and a recipe key, as buildRecipeProduct reads them
    /// (ToolTable.html:3420-3434). The key is a string there; a number is read as its text.
    /// </summary>
    private static void CollectProducts(JsonElement element, List<RecipeProduct> products)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (ProductOf(element) is { } product && products.Count < MaxPreviews) products.Add(product);

                foreach (var property in element.EnumerateObject()) CollectProducts(property.Value, products);

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectProducts(item, products);

                break;
        }
    }

    private static RecipeProduct? ProductOf(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("productAttrDelta", out var preview) || preview.ValueKind != JsonValueKind.String) return null;
        if (!recipe.TryGetProperty("recipeKey", out var key)) return null;

        var text = preview.GetString() ?? "";
        var recipeKey = key.ValueKind switch
        {
            JsonValueKind.String => key.GetString() ?? "",
            JsonValueKind.Number => key.GetRawText(),
            _ => "",
        };

        if (string.IsNullOrWhiteSpace(text) || text.Length > PageCommand.MaxPreviewLength || recipeKey.Length is 0 or > MaxRecipeKeyLength) return null;

        return new RecipeProduct(recipeKey, text);
    }

    private static void Add(string? preview, List<string> previews)
    {
        if (string.IsNullOrWhiteSpace(preview) || preview.Length > PageCommand.MaxPreviewLength) return;
        if (previews.Count >= MaxPreviews || previews.Contains(preview)) return;

        previews.Add(preview);
    }
}
