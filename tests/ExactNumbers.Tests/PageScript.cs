using ExactNumbers.Core;
using Jint;

namespace ExactNumbers.Tests;

/// <summary>
/// page.js exactly as the plugin embeds it, run in Jint on top of FakePage.js (Root.html with its page iframes, the
/// vuplex bridge, an object menu and a crafting table). The plugin's side is the real core, as ExactSession uses it:
/// what the script posts is parsed with PageCommand and answered from a LabelBook. Nothing is answered until a test
/// says so, so a test can hold an answer in flight. With a measure store it answers as a measuring session does: game
/// messages go through PageSites with the menu state and recipe sites the test sets.
/// </summary>
internal sealed class PageScript
{
    private readonly Engine _engine = new();
    private readonly LabelBook _book;
    private readonly bool _isMeasuring;
    private readonly Queue<PageCommand.Game> _games = new();
    private readonly Queue<PageCommand.Need> _needs = new();

    /// <param name="measured">Null answers as with measuring off, as 0.1.0 did.</param>
    public PageScript(MeasureStore? measured = null)
    {
        _book = new LabelBook(measured);
        _isMeasuring = measured != null;
        _engine.Execute(Resource("ExactNumbers.Tests.FakePage.js"));
        _engine.Execute(Resource("ExactNumbers.page.js"));
        Hear();
    }

    /// <summary>The object menu state the plugin reads when it answers a menu message; measuring only.</summary>
    public MenuState? Menu { get; set; }

    /// <summary>The recipe functions the crafting hook noted; measuring only.</summary>
    public RecipeSites Recipes { get; } = new();

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

    /// <summary>One run of the script's 250 ms poll.</summary>
    public void Poll() => Run("poll()");

    /// <summary>Hands every MutationObserver the changes made under what it watches, as the browser does after a change.</summary>
    public void DeliverMutations() => Run("deliverMutations()");

    /// <summary>Runs the animation frame callbacks waiting now, as the browser does before it paints.</summary>
    public void Frame() => Run("frame()");

    public int PendingFrames => (int)Number("animationFrames.length");

    /// <summary>getElementById, querySelector and querySelectorAll calls on every fake page so far.</summary>
    public int Searches => (int)Number("searches");

    /// <summary>The open menu draws its buttons again in its same container, with the game's marks.</summary>
    public void RedrawMenu(string pageId, params string[] buttons) => Run($"redrawMenu({JsText.Literal(pageId)}, [{string.Join(",", buttons)}])");

    public void GameSends(string pageId, string name, string json) => Run($"gameSends({JsText.Literal(pageId)}, {JsText.Literal(name)}, {JsText.Literal(json)})");

    /// <param name="buttons">Per button, a JavaScript list of the chips the game draws for it.</param>
    public void OpenMenu(string pageId, params string[] buttons) => Run($"openMenu({JsText.Literal(pageId)}, [{string.Join(",", buttons)}])");

    public string MenuChip(string pageId, int button, int chip) => Text($"menuChip({JsText.Literal(pageId)}, {button}, {chip})");

    /// <param name="draws">A JavaScript object: preview text -> the chips the game draws for it.</param>
    public void OpenToolTable(string draws) => Run($"openToolTable({draws})");

    public void HoverRecipe(params string[] previews) => Run($"hoverRecipe([{string.Join(",", previews.Select(JsText.Literal))}])");

    public string RecipeChip(int wrap, int chip) => Text($"recipeChip({wrap}, {chip})");

    public int RecipeBuilds => (int)Number("toolTable().builds");

    /// <param name="recipes">A JavaScript list of {recipeKey, productAttrDelta}, drawn as the recipe list.</param>
    public void DrawRecipes(string recipes) => Run($"drawRecipes({recipes})");

    public void HoverProduct(string recipeKey) => Run($"hoverProduct({JsText.Literal(recipeKey)})");

    /// <summary>The label keys the script asked numbers for and has not been answered for yet.</summary>
    public IReadOnlyList<string> Needs => _needs.Select(need => need.Key).ToList();

    /// <summary>Answers every game message passed on so far, in order, as the plugin does.</summary>
    public void AnswerGameMessages()
    {
        while (_games.TryDequeue(out var game)) Run("window.__slexactnumbers.takeGame(" + Answer(game) + ")");
    }

    /// <summary>Answers every request for numbers so far, as the plugin does.</summary>
    public void AnswerNeeds()
    {
        while (_needs.TryDequeue(out var need)) Run("window.__slexactnumbers.setLabels(" + _book.Answer(need.Key) + ")");
    }

    /// <summary>Sends one key's numbers unasked, as the plugin's answer to someone else's request would.</summary>
    public void SendLabels(string key) => Run("window.__slexactnumbers.setLabels(" + _book.Answer(key) + ")");

    /// <summary>Resends the numbers of these functions' keys the page holds, as the session does after a new measurement.</summary>
    public void Remeasured(params MeasureSite[] sites)
    {
        var resend = _book.Remeasured(sites);

        if (resend != null) Run("window.__slexactnumbers.setLabels(" + resend + ")");
    }

    private string Answer(PageCommand.Game game)
    {
        var facts = GameMessage.Read(game.Message);

        return _isMeasuring ? _book.AnswerGame(game.Seq, PageSites.Answer(facts, Menu, Recipes)) : _book.AnswerGame(game.Seq, facts);
    }

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
