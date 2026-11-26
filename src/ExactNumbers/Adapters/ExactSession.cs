using System;
using ExactNumbers.Core;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
using GameCore.HotUpdate.Battle.Logic;

namespace ExactNumbers.Adapters;

/// <summary>
/// Measuring, which the Measure setting turns on and off while the game runs. The stat hooks go in the first time it is
/// turned on and stay in; while it is off they find no measurer and return at once. The measurements file is read the
/// first time too, and its store is kept for the rest of the session, so turning measuring off and on reads nothing.
/// </summary>
internal sealed class Measuring
{
    private readonly Func<bool> _hookStats;
    private bool? _isHooked;

    /// <param name="hookStats">Puts the stat hooks in; false when they could not all be put in.</param>
    public Measuring(MeasuredFile file, RecipeSites recipes, TraceSwitch trace, Func<bool> hookStats)
    {
        File = file;
        Recipes = recipes;
        Trace = trace;
        _hookStats = hookStats;
    }

    public MeasuredFile File { get; }

    /// <summary>The recipe map the crafting hooks fill, measuring on or off.</summary>
    public RecipeSites Recipes { get; }

    public TraceSwitch Trace { get; }

    /// <summary>The measurements; null until measuring is first turned on.</summary>
    public MeasureStore? Store { get; private set; }

    /// <summary>What the hooks feed; null while measuring is off.</summary>
    public Measurer? Measurer { get; private set; }

    /// <summary>On the main thread, under the gate.</summary>
    /// <returns>False when the stat hooks could not be put in; measuring then stays off for the session.</returns>
    public bool TurnOn()
    {
        if (Measurer != null) return true;

        _isHooked ??= _hookStats();

        if (_isHooked != true) return false;

        Store ??= File.Load();
        // A new measurer has no use open: what the character did while measuring was off was not watched.
        Measurer = new Measurer(Store, Trace.Write, Trace.WriteCost);
        MeasureHooks.Use(Measurer);

        return true;
    }

    /// <summary>On the main thread, under the gate.</summary>
    public void TurnOff()
    {
        if (Measurer == null) return;

        Measurer = null;
        MeasureHooks.Use(null);
    }
}

/// <summary>
/// The mod's game side, run once a frame. It keeps the page script installed, answers its requests for numbers and
/// resends the numbers on the page whenever the character's modifiers or the game's language change or a furniture
/// function gets a new measurement. With measuring on it also tells the hooks who the player's character is, and once
/// measuring has been on it saves the measurements now and then. It only reads; nothing here changes the game.
/// </summary>
internal sealed class ExactSession
{
    private const double ModifierCheckSeconds = 1;
    private const double LanguageCheckSeconds = 1;
    private const double SaveSeconds = 10;

    /// <summary>
    /// How often the hooks are told who the player's character is. It changes only when a world loads or closes, and no
    /// object can be used within a quarter second of that.
    /// </summary>
    private const double HeroCheckSeconds = 0.25;

    /// <summary>How long a closing game waits for a save already being written before it saves the last changes.</summary>
    private static readonly TimeSpan QuitWait = TimeSpan.FromSeconds(5);

    private const string TurnOnCall = "setOn(true)";
    private const string TurnOffCall = "setOn(false)";

    private readonly WebPage _page;
    private readonly PageMailbox _messages = PageInbox.Listen(PageCommand.Prefix);
    private readonly GameModifiers _modifiers;
    private readonly Measuring _measuring;
    private readonly LabelBook _book = new();
    private readonly Cadence _modifierCheck = new(ModifierCheckSeconds);
    private readonly Cadence _languageCheck = new(LanguageCheckSeconds);
    private readonly Func<bool> _isChinese;
    private readonly Cadence _save = new(SaveSeconds);
    private readonly Cadence _heroCheck = new(HeroCheckSeconds);
    private readonly FileWorker _files = new();
    private readonly Action<string> _log;
    private bool _isOn;
    private bool _hasStarted;
    private bool _isPageReady;
    private bool _isApplyingModifiers = true;
    private bool _hasReportedMenuFailure;

    /// <summary>Whether the save before this one ran while measuring was on, so one more save after it is turned off writes the last changes.</summary>
    private bool _wasMeasuringAtLastSave;

    /// <param name="isChinese">Whether the game is in Simplified Chinese right now; the chips' words follow it.</param>
    public ExactSession(WebPage page, GameModifiers modifiers, Measuring measuring, Func<bool> isChinese, Action<string> log)
    {
        _page = page;
        _modifiers = modifiers;
        _measuring = measuring;
        _isChinese = isChinese;
        _log = log;
    }

    /// <summary>Called on the main thread after each change of the setting. Off, every chip goes back to the base number at the next check.</summary>
    public void UseModifiers(bool isApplying) => _isApplyingModifiers = isApplying;

    /// <summary>
    /// Called on the main thread when the game starts and after each change of the Enabled, Measure or TraceLog
    /// setting. Turned off, the page script puts back every chip it changed and stops; turned on, it says ready again
    /// and asks for its numbers.
    /// </summary>
    public void Use(bool isEnabled, bool measure, bool traceLog)
    {
        lock (MeasureHooks.Gate)
        {
            var isMeasuring = isEnabled && measure && _measuring.TurnOn();

            if (!isMeasuring) _measuring.TurnOff();

            _measuring.Trace.Use(isMeasuring && traceLog);

            var resend = _book.UseMeasured(isMeasuring ? _measuring.Store : null);

            _log($"enabled {isEnabled}, measuring {isMeasuring}, trace log {_measuring.Trace.IsOn}");

            var wasOn = _isOn;

            _isOn = isEnabled;

            if (!isEnabled) _isPageReady = false;

            // At the game start there is no page script yet: KeepInstalled sends it once the mod is on.
            if (_hasStarted && wasOn != isEnabled) _page.Call(isEnabled ? TurnOnCall : TurnOffCall);
            else if (resend != null && _isPageReady) SendLabels(resend);

            _hasStarted = true;
        }
    }

    public void Update()
    {
        var now = Clock.Now;

        if (_isOn) _page.KeepInstalled(now);

        // The hooks hold the same lock, so the store never changes under a payload being written.
        lock (MeasureHooks.Gate)
        {
            SaveNow(now);

            if (!_isOn)
            {
                TakePageCommandsWhileOff();

                return;
            }

            if (_measuring.Measurer != null) TrackMeasuring(now);

            CheckModifiers(now);
            CheckLanguage(now);
            TakePageCommands();
        }
    }

    /// <summary>Saves what is not saved yet and writes out the trace; for the game closing.</summary>
    public void Quit()
    {
        var store = _measuring.Store;

        if (store == null) return;

        // The last batch may still be writing; the final save goes after it, so the newer file is the one kept. One stuck
        // past the wait is left alone rather than written over halfway.
        if (!_files.Finish(QuitWait)) return;

        MeasureSnapshot? snapshot;

        lock (MeasureHooks.Gate) snapshot = store.IsDirty ? store.Snapshot() : null;

        if (snapshot != null) _measuring.File.Save(snapshot, store);

        _measuring.Trace.Flush();
        _measuring.File.ReportFailure();
    }

    private void TrackMeasuring(double now)
    {
        if (_heroCheck.IsDue(now))
        {
            _heroCheck.Ran(now);
            MeasureHooks.SetHero(HeroId());
        }

        var resend = _book.Remeasured(_measuring.Measurer!.TakeChanged());

        if (resend != null && _isPageReady) SendLabels(resend);
    }

    /// <summary>
    /// Under the gate, once measuring has been on: every few seconds, saves the store if it has unsaved changes and
    /// writes out the trace. A save still being written holds the next one back until it is done; nothing is lost, it
    /// is only later. After measuring is turned off, one more save writes what was left.
    /// </summary>
    private void SaveNow(double now)
    {
        var store = _measuring.Store;

        if (store == null || !_save.IsDue(now) || _files.IsBusy) return;

        _save.Ran(now);
        _measuring.File.ReportFailure();

        var isMeasuring = _measuring.Measurer != null;
        var snapshot = store.IsDirty ? store.Snapshot() : null;

        if (snapshot == null && !isMeasuring && !_wasMeasuringAtLastSave) return;

        _wasMeasuringAtLastSave = isMeasuring;

        var file = _measuring.File;
        var trace = _measuring.Trace;

        _files.Start(() =>
        {
            if (snapshot != null) file.Save(snapshot, store);

            trace.Flush();
        });
    }

    /// <summary>AgentManager.LeadingRoleId, read the way sl-action-speed reads it; -1 when no world is loaded.</summary>
    private static long HeroId() => GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.LeadingRoleId ?? -1;

    /// <summary>Looked at before the page's requests, so a request is answered with the character's current modifiers.</summary>
    private void CheckModifiers(double now)
    {
        if (!_modifierCheck.IsDue(now)) return;

        _modifierCheck.Ran(now);

        var modifiers = _isApplyingModifiers ? _modifiers.Read(_book.Current) : Modifiers.None;

        if (modifiers.SameAs(_book.Current)) return;

        _log("modifiers now: " + modifiers.Describe());

        var resend = _book.Update(modifiers);

        if (resend != null && _isPageReady) SendLabels(resend);
    }

    /// <summary>Looked at before the page's requests too, so a language switch rewrites the words already on the page.</summary>
    private void CheckLanguage(double now)
    {
        if (!_languageCheck.IsDue(now)) return;

        _languageCheck.Ran(now);

        var resend = _book.UpdateLanguage(_isChinese());

        if (resend != null && _isPageReady) SendLabels(resend);
    }

    private void TakePageCommands()
    {
        while (_messages.TryTake(out var message))
        {
            var command = PageCommand.Parse(message);

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
                    SendLabels(_book.Answer(need.Key));
                    break;
                case PageCommand.Game game:
                    SendGame(AnswerGame(game));
                    break;
                case PageCommand.Unknown unknown:
                    _log("page said: " + unknown.Text);
                    break;
                default:
                    throw new InvalidOperationException("unhandled page command " + command);
            }
        }
    }

    /// <summary>
    /// While the mod is off nothing is answered. A page script that still talks missed the call that turned it off, so
    /// it is told again; a missing one is left missing.
    /// </summary>
    private void TakePageCommandsWhileOff()
    {
        var isScriptTalking = false;

        while (_messages.TryTake(out var message))
        {
            switch (PageCommand.Parse(message))
            {
                case PageCommand.NoScript:
                    break;
                case PageCommand.Unknown unknown:
                    _log("page said: " + unknown.Text);
                    break;
                default:
                    isScriptTalking = true;
                    break;
            }
        }

        if (isScriptTalking) _page.Call(TurnOffCall);
    }

    /// <summary>Labels by preview text, as built by PagePayload.Labels.</summary>
    private void SendLabels(string json) => _page.Call("setLabels(" + json + ")");

    /// <summary>The answer to a passed-on game message, as built by PagePayload.Game.</summary>
    private void SendGame(string json) => _page.Call("takeGame(" + json + ")");

    private string AnswerGame(PageCommand.Game game)
    {
        var facts = GameMessage.Read(game.Message);

        if (_measuring.Measurer == null) return _book.AnswerGame(game.Seq, facts);

        var state = facts.Menu == null ? null : ReadMenu();
        var answer = PageSites.Answer(facts, state, _measuring.Recipes);

        if (facts.Menu != null) _measuring.Trace.Write(TraceText.Menu(facts.Menu.PageId, state, answer.Reads));
        if (facts.Products.Count > 0) _measuring.Trace.Write(TraceText.Recipes(facts.Products.Count, answer.Recipes));

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
