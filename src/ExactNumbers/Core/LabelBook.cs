using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// Answers the page's requests for numbers and remembers which previews it asked about, so that when the character's
/// modifiers change every number already on the page can be sent again.
/// </summary>
public sealed class LabelBook
{
    /// <summary>The game has a few hundred actions at most; past this a preview is still answered, only not resent.</summary>
    public const int MaxRemembered = 512;

    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);
    private Modifiers _modifiers = Modifiers.None;

    /// <summary>The modifiers the numbers are made with.</summary>
    public Modifiers Current => _modifiers;

    /// <summary>The payload with this preview's labels, under the current modifiers.</summary>
    public string Answer(string preview)
    {
        if (_asked.Count < MaxRemembered) _asked.Add(preview);

        return PagePayload.Labels(new[] { preview }, _modifiers);
    }

    /// <returns>The payload to resend when the modifiers changed and the page holds numbers made with the old ones; otherwise null.</returns>
    public string? Update(Modifiers modifiers)
    {
        if (modifiers.SameAs(_modifiers)) return null;

        _modifiers = modifiers;

        if (_asked.Count == 0) return null;

        return PagePayload.Labels(_asked.OrderBy(preview => preview, StringComparer.Ordinal), _modifiers);
    }

    /// <summary>A fresh page script holds no numbers, so nothing it was sent before needs resending.</summary>
    public void PageReloaded() => _asked.Clear();
}
