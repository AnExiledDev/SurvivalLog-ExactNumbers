using System;
using System.IO;
using ExactNumbers.Core;
using GameCore.HotUpdate.ReduxUI;

namespace ExactNumbers.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in. It
/// puts the numbers into the tooltip chips. The web view can reload at any time and lose the script, so it is checked
/// for every couple of seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "ExactNumbers.page.js";
    private const string Handle = "window.__slexactnumbers";
    private const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    private readonly Action<string> _log;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run(Handle + "||" + NoScriptReply);
    }

    public void Install()
    {
        if (Run(Script())) _log("page script sent to the web UI");
    }

    /// <summary>Labels by preview text, as built by PagePayload.Labels.</summary>
    public void Labels(string json) => Call("setLabels(" + json + ")");

    /// <summary>The answer to a passed-on game message, as built by PagePayload.Game.</summary>
    public void Game(string json) => Call("takeGame(" + json + ")");

    /// <summary>Does nothing when the script is not there; the next check puts it back and it asks again.</summary>
    private void Call(string call) => Run(Handle + "&&" + Handle + "." + call);

    // ReduxUISystem.GetWebUILayer and the layer's canvasWebViewPrefab are how sl-info-pack reaches the same web view.
    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
