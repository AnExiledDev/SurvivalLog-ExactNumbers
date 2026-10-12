using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>Which of a stat's two ratios applies: the game uses the gain ratio for increases and the cost ratio for decreases.</summary>
public readonly record struct RatioKey(Stat Stat, bool IsGain);

/// <summary>The player's character's current ratios. A ratio that could not be read is absent, and its chips show the base.</summary>
public sealed class Modifiers
{
    public static readonly Modifiers None = new(new Dictionary<RatioKey, float>());

    private readonly Dictionary<RatioKey, float> _ratios;

    public Modifiers(IReadOnlyDictionary<RatioKey, float> ratios)
    {
        _ratios = new Dictionary<RatioKey, float>(ratios);
    }

    public float? RatioFor(Stat stat, bool isGain) => _ratios.TryGetValue(new RatioKey(stat, isGain), out var ratio) ? ratio : null;

    /// <summary>Exact comparison on purpose: any change at all means the numbers on the page are stale.</summary>
    public bool SameAs(Modifiers other) =>
        _ratios.Count == other._ratios.Count && _ratios.All(pair => other._ratios.TryGetValue(pair.Key, out var ratio) && ratio.Equals(pair.Value));

    /// <summary>For the log: every ratio that was read, in a fixed order.</summary>
    public string Describe()
    {
        if (_ratios.Count == 0) return "none read";

        return string.Join(", ", _ratios
            .OrderBy(pair => pair.Key.Stat)
            .ThenBy(pair => pair.Key.IsGain)
            .Select(pair => $"{pair.Key.Stat} {(pair.Key.IsGain ? "gain" : "cost")} {pair.Value.ToString("0.####", CultureInfo.InvariantCulture)}"));
    }
}
