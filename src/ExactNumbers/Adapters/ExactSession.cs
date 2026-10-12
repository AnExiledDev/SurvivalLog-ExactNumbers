using System;
using ExactNumbers.Core;
using UnityEngine;

namespace ExactNumbers.Adapters;

/// <summary>
/// The mod's game side, run once a frame. It keeps the page script installed, answers its requests for numbers and,
/// with modifiers on, resends the numbers on the page whenever the character's modifiers change. It only reads; nothing
/// here changes the game.
/// </summary>
internal sealed class ExactSession
{
    private const float ModifierCheckSeconds = 1f;

    private readonly WebPage _page;
    private readonly GameModifiers? _modifiers;
    private readonly LabelBook _book = new();
    private readonly Cadence _modifierCheck = new(ModifierCheckSeconds);
    private readonly Action<string> _log;
    private bool _isPageReady;

    /// <param name="modifiers">Null when modifiers are off: every chip shows the base number.</param>
    public ExactSession(WebPage page, GameModifiers? modifiers, Action<string> log)
    {
        _page = page;
        _modifiers = modifiers;
        _log = log;
    }

    public void Update()
    {
        var now = Time.unscaledTime;

        _page.KeepInstalled(now);
        CheckModifiers(now);
        TakePageCommands();
    }

    /// <summary>Looked at before the page's requests, so a request is answered with the character's current modifiers.</summary>
    private void CheckModifiers(float now)
    {
        if (_modifiers == null || !_modifierCheck.IsDue(now)) return;

        _modifierCheck.Ran(now);

        var modifiers = _modifiers.Read();

        if (modifiers.SameAs(_book.Current)) return;

        _log("modifiers now: " + modifiers.Describe());

        var resend = _book.Update(modifiers);

        if (resend != null && _isPageReady) _page.Labels(resend);
    }

    private void TakePageCommands()
    {
        while (PageMessages.TryTake(out var command))
        {
            switch (command)
            {
                case PageCommand.Ready:
                    _log("page script ready");
                    _isPageReady = true;
                    _book.PageReloaded();
                    break;
                case PageCommand.NoScript:
                    _isPageReady = false;
                    _page.Install();
                    break;
                case PageCommand.Need need:
                    _page.Labels(_book.Answer(need.Preview));
                    break;
                case PageCommand.Game game:
                    _page.Game(_book.AnswerGame(game.Seq, GameMessage.Read(game.Message)));
                    break;
                case PageCommand.Unknown unknown:
                    _log("page said: " + unknown.Text);
                    break;
                default:
                    throw new InvalidOperationException("unhandled page command " + command);
            }
        }
    }
}
