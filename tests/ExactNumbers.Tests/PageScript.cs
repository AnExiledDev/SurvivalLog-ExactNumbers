using ExactNumbers.Core;
using Jint;

namespace ExactNumbers.Tests;

/// <summary>
/// page.js exactly as the plugin embeds it, run in Jint on top of FakePage.js (Root.html with its page iframes, the
/// vuplex bridge, an object menu and a crafting table). The plugin's side is the real core, as ExactSession uses it:
/// what the script posts is parsed with PageCommand and answered from a LabelBook. Nothing is answered until a test
/// says so, so a test can hold an answer in flight.
/// </summary>
internal sealed class PageScript
{
    private readonly Engine _engine = new();
    private readonly LabelBook _book = new();
    private readonly Queue<PageCommand.Game> _games = new();
    private readonly Queue<PageCommand.Need> _needs = new();

    public PageScript()
    {
        _engine.Execute(Resource("ExactNumbers.Tests.FakePage.js"));
        _engine.Execute(Resource("ExactNumbers.page.js"));
        Hear();
    }

    /// <summary>Every game message the script passed on to the plugin, as Root.html received it.</summary>
    public List<string> PassedOn { get; } = new();

    /// <summary>Anything else the script said, such as its "diag|" notes when something threw.</summary>
    public List<string> Notes { get; } = new();

    public void Run(string statements)
    {
        _engine.Execute(statements);
        Hear();
    }

    public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    public double Number(string expression) => _engine.Evaluate($"Number({expression})").AsNumber();

    /// <summary>One run of the script's 200 ms poll.</summary>
    public void Poll() => Run("poll()");

    public void GameSends(string pageId, string name, string json) => Run($"gameSends({JsText.Literal(pageId)}, {JsText.Literal(name)}, {JsText.Literal(json)})");

    /// <param name="buttons">Per button, a JavaScript list of the chips the game draws for it.</param>
    public void OpenMenu(string pageId, params string[] buttons) => Run($"openMenu({JsText.Literal(pageId)}, [{string.Join(",", buttons)}])");

    public string MenuChip(string pageId, int button, int chip) => Text($"menuChip({JsText.Literal(pageId)}, {button}, {chip})");

    /// <param name="draws">A JavaScript object: preview text -> the chips the game draws for it.</param>
    public void OpenToolTable(string draws) => Run($"openToolTable({draws})");

    public void HoverRecipe(params string[] previews) => Run($"hoverRecipe([{string.Join(",", previews.Select(JsText.Literal))}])");

    public string RecipeChip(int wrap, int chip) => Text($"recipeChip({wrap}, {chip})");

    public int RecipeBuilds => (int)Number("toolTable().builds");

    /// <summary>Answers every game message passed on so far, in order, as the plugin does.</summary>
    public void AnswerGameMessages()
    {
        while (_games.TryDequeue(out var game)) Run("window.__slexactnumbers.takeGame(" + _book.AnswerGame(game.Seq, GameMessage.Read(game.Message)) + ")");
    }

    /// <summary>Answers every request for numbers so far, as the plugin does.</summary>
    public void AnswerNeeds()
    {
        while (_needs.TryDequeue(out var need)) Run("window.__slexactnumbers.setLabels(" + _book.Answer(need.Preview) + ")");
    }

    /// <summary>Sends one preview's numbers unasked, as the plugin's answer to someone else's request would.</summary>
    public void SendLabels(string preview) => Run("window.__slexactnumbers.setLabels(" + _book.Answer(preview) + ")");

    /// <summary>Takes what the script posted since the last look.</summary>
    private void Hear()
    {
        var count = (int)_engine.Evaluate("posted.length").AsNumber();
        var messages = Enumerable.Range(0, count).Select(i => _engine.Evaluate($"posted[{i}]").AsString()).ToList();

        _engine.Execute("posted.length = 0");

        foreach (var message in messages) Take(PageCommand.Parse(message));
    }

    private void Take(PageCommand command)
    {
        switch (command)
        {
            case PageCommand.Ready:
                _book.PageReloaded();
                break;
            case PageCommand.Game game:
                PassedOn.Add(game.Message);
                _games.Enqueue(game);
                break;
            case PageCommand.Need need:
                _needs.Enqueue(need);
                break;
            case PageCommand.Unknown unknown:
                Notes.Add(unknown.Text);
                break;
            default:
                Notes.Add(command.ToString());
                break;
        }
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PageScript).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
