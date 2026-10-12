using System;
using ExactNumbers.Core;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace ExactNumbers.Adapters;

/// <summary>
/// What measuring needs on the session side: the measurer the hooks feed, the file its store is kept in, the recipe map
/// the crafting hook fills, and where trace lines go.
/// </summary>
internal sealed record Measuring(Measurer Measurer, MeasuredFile File, RecipeSites Recipes, Action<string> Trace);

/// <summary>
/// The mod's game side, run once a frame. It keeps the page script installed, answers its requests for numbers and
/// resends the numbers on the page whenever the character's modifiers change or a furniture function gets a new
/// measurement. With measuring on it also tells the hooks who the player's character is and saves the measurements now
/// and then. It only reads; nothing here changes the game.
/// </summary>
internal sealed class ExactSession
{
    private const float ModifierCheckSeconds = 1f;
    private const float SaveSeconds = 10f;

    private readonly WebPage _page;
    private readonly GameModifiers? _modifiers;
    private readonly Measuring? _measuring;
    private readonly LabelBook _book;
    private readonly Cadence _modifierCheck = new(ModifierCheckSeconds);
    private readonly Cadence _save = new(SaveSeconds);
    private readonly Action<string> _log;
    private bool _isPageReady;
    private bool _hasReportedMenuFailure;

    /// <param name="modifiers">Null when modifiers are off: every chip shows the base number.</param>
    /// <param name="measuring">Null when measuring is off: every chip shows the game's estimate, as in 0.1.0.</param>
    public ExactSession(WebPage page, GameModifiers? modifiers, Measuring? measuring, Action<string> log)
    {
        _page = page;
        _modifiers = modifiers;
        _measuring = measuring;
        _book = new LabelBook(measuring?.Measurer.Store);
        _log = log;
    }

    public void Update()
    {
        var now = Time.unscaledTime;

        _page.KeepInstalled(now);

        // The hooks hold the same lock, so the store never changes under a payload being written.
        lock (MeasureHooks.Gate)
        {
            if (_measuring != null) TrackMeasuring(_measuring, now);

            CheckModifiers(now);
            TakePageCommands();
        }
    }

    /// <summary>Saves what is not saved yet; for the game closing.</summary>
    public void Quit()
    {
        if (_measuring == null) return;

        lock (MeasureHooks.Gate) SaveIfChanged(_measuring);
    }

    private void TrackMeasuring(Measuring measuring, float now)
    {
        MeasureHooks.SetHero(HeroId());

        var resend = _book.Remeasured(measuring.Measurer.TakeChanged());

        if (resend != null && _isPageReady) _page.Labels(resend);

        if (!_save.IsDue(now)) return;

        _save.Ran(now);
        SaveIfChanged(measuring);
    }

    private static void SaveIfChanged(Measuring measuring)
    {
        if (measuring.Measurer.Store.IsDirty) measuring.File.Save(measuring.Measurer.Store);
    }

    /// <summary>AgentManager.LeadingRoleId, read the way sl-action-speed reads it; -1 when no world is loaded.</summary>
    private static long HeroId() => GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.LeadingRoleId ?? -1;

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
                    _page.Labels(_book.Answer(need.Key));
                    break;
                case PageCommand.Game game:
                    _page.Game(AnswerGame(game));
                    break;
                case PageCommand.Unknown unknown:
                    _log("page said: " + unknown.Text);
                    break;
                default:
                    throw new InvalidOperationException("unhandled page command " + command);
            }
        }
    }

    private string AnswerGame(PageCommand.Game game)
    {
        var facts = GameMessage.Read(game.Message);

        if (_measuring == null) return _book.AnswerGame(game.Seq, facts);

        var state = facts.Menu == null ? null : ReadMenu();
        var answer = PageSites.Answer(facts, state, _measuring.Recipes);

        if (facts.Menu != null) _measuring.Trace(TraceText.Menu(facts.Menu.PageId, state, answer.Reads));
        if (facts.Products.Count > 0) _measuring.Trace(TraceText.Recipes(facts.Products.Count, answer.Recipes));

        return _book.AnswerGame(game.Seq, answer);
    }

    /// <summary>Never throws: without the menu state, menu chips keep the game's estimates.</summary>
    private MenuState? ReadMenu()
    {
        try
        {
            return MenuReader.Read();
        }
        catch (Exception ex)
        {
            if (!_hasReportedMenuFailure) _log("could not read the object menu's state, its chips keep the game's estimates (reported once): " + ex);

            _hasReportedMenuFailure = true;

            return null;
        }
    }
}
