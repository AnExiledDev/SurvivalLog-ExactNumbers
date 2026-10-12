using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>
/// Answers the page's requests for numbers and remembers which label keys it was sent, so that when the character's
/// modifiers change, or a furniture function gets a new measurement, every number already on the page can be sent again.
/// </summary>
public sealed class LabelBook
{
    /// <summary>The game has a few hundred actions at most; past this a key is still answered, only not resent.</summary>
    public const int MaxRemembered = 512;

    private readonly HashSet<string> _sent = new(StringComparer.Ordinal);
    private readonly MeasureStore? _measured;
    private Modifiers _modifiers = Modifiers.None;

    /// <param name="measured">What was measured, or null to show the game's estimates only, as 0.1.0 did.</param>
    public LabelBook(MeasureStore? measured = null)
    {
        _measured = measured;
    }

    /// <summary>The modifiers the numbers are made with.</summary>
    public Modifiers Current => _modifiers;

    /// <summary>The payload with this key's labels, under the current modifiers.</summary>
    public string Answer(string key)
    {
        Remember(key);

        return PagePayload.Labels(new[] { key }, _modifiers, _measured);
    }

    /// <summary>
    /// The answer to a game message the page passed on: always given, even when the message held nothing, so the page
    /// knows it was read. It carries numbers only for keys the page was not sent since it loaded; the page keeps the
    /// rest, and Update and Remeasured resend them when they change.
    /// </summary>
    public string AnswerGame(int seq, GameAnswer answer)
    {
        var unsent = answer.Keys.Where(key => !_sent.Contains(key)).ToList();

        foreach (var key in unsent) Remember(key);

        return PagePayload.Game(seq, answer, unsent, _modifiers, _measured);
    }

    /// <summary>The same with no furniture functions known, as when measuring is off.</summary>
    public string AnswerGame(int seq, GameFacts facts) => AnswerGame(seq, GameAnswer.Plain(facts));

    /// <returns>The payload to resend when the modifiers changed and the page holds numbers made with the old ones; otherwise null.</returns>
    public string? Update(Modifiers modifiers)
    {
        if (modifiers.SameAs(_modifiers)) return null;

        _modifiers = modifiers;

        if (_sent.Count == 0) return null;

        return PagePayload.Labels(_sent.OrderBy(key => key, StringComparer.Ordinal), _modifiers, _measured);
    }

    /// <returns>The payload to resend for the keys of these furniture functions the page holds; null when it holds none.</returns>
    public string? Remeasured(IEnumerable<MeasureSite> sites)
    {
        var changed = new HashSet<MeasureSite>(sites);
        var keys = _sent
            .Where(key => LabelKey.Parse(key).Site is { } site && changed.Contains(site))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        return keys.Count == 0 ? null : PagePayload.Labels(keys, _modifiers, _measured);
    }

    /// <summary>A fresh page script holds no numbers, so nothing it was sent before needs resending.</summary>
    public void PageReloaded() => _sent.Clear();

    private void Remember(string key)
    {
        if (_sent.Count < MaxRemembered) _sent.Add(key);
    }
}
