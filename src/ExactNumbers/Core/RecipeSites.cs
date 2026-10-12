using System;
using System.Collections.Generic;

namespace ExactNumbers.Core;

/// <summary>Which furniture function a crafting recipe's product preview was taken from, and that preview.</summary>
public sealed record RecipeSite(MeasureSite Site, string Preview);

/// <summary>
/// The furniture function behind each crafting recipe's product chips, noted while the game fills in the recipe list.
/// Bounded: the game has a few hundred recipes, so past the cap new recipes are simply not mapped and keep the estimate.
/// </summary>
public sealed class RecipeSites
{
    public const int MaxRecipes = 4096;

    private readonly Dictionary<string, RecipeSite> _sites = new(StringComparer.Ordinal);

    public int Count => _sites.Count;

    /// <returns>False when the map is full and the recipe is new to it.</returns>
    public bool Note(string recipeKey, RecipeSite site)
    {
        if (!_sites.ContainsKey(recipeKey) && _sites.Count >= MaxRecipes) return false;

        _sites[recipeKey] = site;

        return true;
    }

    public void Forget(string recipeKey) => _sites.Remove(recipeKey);

    /// <returns>The site only while the recipe still shows the preview it was noted with.</returns>
    public MeasureSite? For(string recipeKey, string preview) =>
        _sites.TryGetValue(recipeKey, out var noted) && noted.Preview == preview ? noted.Site : null;
}
