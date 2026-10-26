using System;
using ExactNumbers.Core;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace ExactNumbers.Adapters;

/// <summary>
/// What measuring needs on the session side: the measurer the hooks feed, the file its store is kept in, the recipe map
/// the crafting hook fills, where trace lines go, and how the trace lines kept in memory are written to the file.
/// </summary>
internal sealed record Measuring(Measurer Measurer, MeasuredFile File, RecipeSites Recipes, Action<string> Trace, Action FlushTrace);

/// <summary>
/// The mod's game side, run once a frame. It keeps the page script installed, answers its requests for numbers and
/// resends the numbers on the page whenever the character's modifiers or the game's language change or a furniture
/// function gets a new measurement. With measuring on it also tells the hooks who the player's character is and saves
/// the measurements now and then. It only reads; nothing here changes the game.
/// </summary>
internal sealed class ExactSession
{
    private const float ModifierCheckSeconds = 1f;
    private const float LanguageCheckSeconds = 1f;
    private const float SaveSeconds = 10f;

    /// <summary>
    /// How often the hooks are told who the player's character is. It changes only when a world loads or closes, and no
    /// object can be used within a quarter second of that.
    /// </summary>
    private const float HeroCheckSeconds = 0.25f;

    /// <summary>How long a closing game waits for a save already being written before it saves the last changes.</summary>
    private static readonly TimeSpan QuitWait = TimeSpan.FromSeconds(5);

    private readonly WebPage _page;
    private readonly GameModifiers _modifiers;
    private readonly Measuring? _measuring;
    private readonly LabelBook _book;
    private readonly Cadence _modifierCheck = new(ModifierCheckSeconds);
    private readonly Cadence _languageCheck = new(LanguageCheckSeconds);
    private readonly Func<bool> _isChinese;
    private readonly Cadence _save = new(SaveSeconds);
    private readonly Cadence _heroCheck = new(HeroCheckSeconds);
    private readonly FileWorker _files = new();
    private readonly Action<string> _log;
    private bool _isPageReady;
    private bool _isApplyingModifiers = true;
    private bool _hasReportedMenuFailure;

    /// <param name="measuring">Null when measuring is off: every chip shows the game's estimate, as in 0.1.0.</param>
    /// <param name="isChinese">Whether the game is in Simplified Chinese right now; the chips' words follow it.</param>
    public ExactSession(WebPage page, GameModifiers modifiers, Measuring? measuring, Func<bool> isChinese, Action<string> log)
    {
        _page = page;
        _modifiers = modifiers;
        _measuring = measuring;
        _isChinese = isChinese;
        _book = new LabelBook(measuring?.Measurer.Store);
        _log = log;
    }

    /// <summary>Called on the main thread after each change of the setting. Off, every chip goes back to the base number at the next check.</summary>
    public void UseModifiers(bool isApplying) => _isApplyingModifiers = isApplying;

    public void Update()
    {
        var now = Time.unscaledTime;

        _page.KeepInstalled(now);

        // The hooks hold the same lock, so the store never changes under a payload being written.
        lock (MeasureHooks.Gate)
        {
            if (_measuring != null) TrackMeasuring(_measuring, now);

            CheckModifiers(now);
            CheckLanguage(now);
            TakePageCommands();
        }
    }

    /// <summary>Saves what is not saved yet and writes out the trace; for the game closing.</summary>
    public void Quit()
    {
        if (_measuring == null) return;

        // The last batch may still be writing; the final save goes after it, so the newer file is the one kept. One stuck
        // past the wait is left alone rather than written over halfway.
        if (!_files.Finish(QuitWait)) return;

        MeasureSnapshot? snapshot;

        lock (MeasureHooks.Gate) snapshot = SnapshotIfChanged(_measuring);

        if (snapshot != null) _measuring.File.Save(snapshot, _measuring.Measurer.Store);

        _measuring.FlushTrace();
        _measuring.File.ReportFailure();
    }

    private void TrackMeasuring(Measuring measuring, float now)
    {
        if (_heroCheck.IsDue(now))
        {
            _heroCheck.Ran(now);
            MeasureHooks.SetHero(HeroId());
        }

        var resend = _book.Remeasured(measuring.Measurer.TakeChanged());

        if (resend != null && _isPageReady) _page.Labels(resend);

        // A save still being written holds the next one back until it is done; nothing is lost, it is only later.
        if (!_save.IsDue(now) || _files.IsBusy) return;

        _save.Ran(now);
        measuring.File.ReportFailure();

        var snapshot = SnapshotIfChanged(measuring);
        var store = measuring.Measurer.Store;

        _files.Start(() =>
        {
            if (snapshot != null) measuring.File.Save(snapshot, store);

            measuring.FlushTrace();
        });
    }

    /// <summary>Under the gate: the store as it is, or null when it has nothing unsaved.</summary>
    private static MeasureSnapshot? SnapshotIfChanged(Measuring measuring) =>
        measuring.Measurer.Store.IsDirty ? measuring.Measurer.Store.Snapshot() : null;

    /// <summary>AgentManager.LeadingRoleId, read the way sl-action-speed reads it; -1 when no world is loaded.</summary>
    private static long HeroId() => GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.LeadingRoleId ?? -1;

    /// <summary>Looked at before the page's requests, so a request is answered with the character's current modifiers.</summary>
    private void CheckModifiers(float now)
    {
        if (!_modifierCheck.IsDue(now)) return;

        _modifierCheck.Ran(now);

        var modifiers = _isApplyingModifiers ? _modifiers.Read(_book.Current) : Modifiers.None;

        if (modifiers.SameAs(_book.Current)) return;

        _log("modifiers now: " + modifiers.Describe());

        var resend = _book.Update(modifiers);

        if (resend != null && _isPageReady) _page.Labels(resend);
    }

    /// <summary>Looked at before the page's requests too, so a language switch rewrites the words already on the page.</summary>
    private void CheckLanguage(float now)
    {
        if (!_languageCheck.IsDue(now)) return;

        _languageCheck.Ran(now);

        var resend = _book.UpdateLanguage(_isChinese());

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
