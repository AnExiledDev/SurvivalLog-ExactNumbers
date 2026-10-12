using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>One button of the open object menu as the game's menu state holds it: its furniture function and its preview.</summary>
public sealed record MenuButton(int FuncId, string? Preview);

/// <summary>
/// The open object menu as the game's menu state holds it (State_Web_SelectMenu): the furniture's config id and the
/// buttons in the order the menu message lists them.
/// </summary>
public sealed record MenuState(int ConfigId, IReadOnlyList<MenuButton> Buttons);

/// <summary>An object menu with the key the page asks numbers for under each button, "" for a button without a preview.</summary>
public sealed record KeyedMenu(string PageId, IReadOnlyList<string> Keys);

/// <summary>What was read for one menu button, for the trace: the func id the message carried, the state's, and the key chosen.</summary>
public sealed record MenuRead(int Index, int? JsonFunc, int? StateFunc, string Key);

/// <summary>
/// The answer to one passed-on game message: the menu with its keys, each crafting recipe whose product chips have a
/// known furniture function (recipe key to label key), and every label key whose numbers the page may need.
/// </summary>
public sealed record GameAnswer(KeyedMenu? Menu, IReadOnlyDictionary<string, string> Recipes, IReadOnlyList<string> Keys, IReadOnlyList<MenuRead> Reads)
{
    private static readonly IReadOnlyDictionary<string, string> NoRecipes = new Dictionary<string, string>();

    /// <summary>The answer with no furniture functions known: every chip is asked for by its plain preview, as in 0.1.0.</summary>
    public static GameAnswer Plain(GameFacts facts) =>
        new(facts.Menu == null ? null : new KeyedMenu(facts.Menu.PageId, facts.Menu.Previews), NoRecipes, facts.Previews, Array.Empty<MenuRead>());
}

/// <summary>
/// Works out which furniture function each chip in a game message belongs to, so measured numbers go on the right chips.
/// A menu button's function is the one its message carries; failing that, the menu state's button at the same place,
/// but only while the state lists as many buttons and that button shows the same preview, so a state from another
/// menu is never used. The furniture is always the state's. A recipe's function is the one noted when the game filled
/// in its product, while the product still shows that preview. Anything not known keeps its plain preview as the key.
/// </summary>
public static class PageSites
{
    public static GameAnswer Answer(GameFacts facts, MenuState? state, RecipeSites recipes)
    {
        var reads = MenuReads(facts.Menu, state);
        var menu = facts.Menu == null ? null : new KeyedMenu(facts.Menu.PageId, reads.Select(read => read.Key).ToList());
        var recipeKeys = RecipeKeys(facts.Products, recipes);
        var keys = facts.Previews
            .Concat(menu?.Keys ?? Array.Empty<string>())
            .Concat(recipeKeys.Values)
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new GameAnswer(menu, recipeKeys, keys, reads);
    }

    private static List<MenuRead> MenuReads(PageMenu? menu, MenuState? state)
    {
        var reads = new List<MenuRead>();

        if (menu == null) return reads;

        var usable = state != null && state.ConfigId > 0 && state.Buttons.Count == menu.Previews.Count ? state : null;

        for (var i = 0; i < menu.Previews.Count; i++)
        {
            var preview = menu.Previews[i];
            var jsonFunc = i < menu.FuncIds.Count ? menu.FuncIds[i] : null;
            var stateFunc = usable == null ? null : StateFunc(usable.Buttons[i], preview);
            var func = jsonFunc ?? stateFunc ?? 0;
            var site = usable == null ? (MeasureSite?)null : new MeasureSite(usable.ConfigId, func);

            reads.Add(new MenuRead(i, jsonFunc, stateFunc, LabelKey.For(site, preview)));
        }

        return reads;
    }

    private static int? StateFunc(MenuButton button, string preview)
    {
        if (button.FuncId <= 0) return null;

        return button.Preview == null || button.Preview == preview ? button.FuncId : null;
    }

    private static Dictionary<string, string> RecipeKeys(IReadOnlyList<RecipeProduct> products, RecipeSites recipes)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var product in products)
        {
            if (recipes.For(product.RecipeKey, product.Preview) is { } site) keys[product.RecipeKey] = LabelKey.For(site, product.Preview);
        }

        return keys;
    }
}
