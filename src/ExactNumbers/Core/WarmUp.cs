using System;

namespace ExactNumbers.Core;

/// <summary>
/// Runs everything the session runs for its first menu, its first measurement and its first save once, on throwaway
/// objects, so the runtime compiles that code and builds its regex and JSON machinery before the player opens a menu.
/// Without it the first menu of a session pays for all of that on the game's frame. Pure .NET and touches nothing
/// shared, so it can run on any thread.
/// </summary>
public static class WarmUp
{
    private const int Furniture = 500;
    private const int Func = 301;
    private const int ActionId = 11;

    /// <returns>The answer the page would get for the warm-up menu; for the test that it went all the way through.</returns>
    public static string Run()
    {
        var store = new MeasureStore();
        var measurer = new Measurer(store, _ => { });

        Measure(measurer);

        var book = new LabelBook(store);
        var site = new MeasureSite(Furniture, Func);
        var json = "{\"btnList\":[{\"attrDelta\":\"Satiety:-12;MaxStamina:+5\",\"funcId\":" + Func + "}],\"recipes\":[{\"recipeKey\":\"r1\",\"productAttrDelta\":\"Morale:+3\"}]}";
        var raw = string.Join('\u001e', "8", "SelectMenu", GameMessage.MenuMessage, json);

        if (PageCommand.Parse(PageCommand.Prefix + "game|1|" + raw) is not PageCommand.Game game) throw new InvalidOperationException("warm-up message did not parse");

        var facts = GameMessage.Read(game.Message);
        var recipes = new RecipeSites();

        recipes.Note("r1", new RecipeSite(site, "Morale:+3"));

        var answer = PageSites.Answer(facts, new MenuState(Furniture, new[] { new MenuButton(Func, "Satiety:-12;MaxStamina:+5") }), recipes);
        var payload = book.AnswerGame(game.Seq, answer);

        PageCommand.Parse(PageCommand.Prefix + "need|" + LabelKey.For(site, "Satiety:-12"));
        book.Answer("Satiety:-12");
        book.Remeasured(new[] { site });
        book.UpdateLanguage(true);
        TraceText.Menu("SelectMenu", null, answer.Reads);
        TraceText.Recipes(facts.Products.Count, answer.Recipes);

        var file = MeasureFile.Write(store.Snapshot(), "warm-up", Array.Empty<OtherVersion>());

        MeasureFile.Read(file, "warm-up");

        return payload;
    }

    /// <summary>
    /// One use of the warm-up function that drops satiety by 12, start to end, through the measurer. Its trace is a
    /// line-taking one, so the cost line is built as the game's trace builds it.
    /// </summary>
    private static void Measure(Measurer measurer)
    {
        var call = new CostCall(
            nameof(Stat.Satiety), Stat.Satiety, -12, 1, "Action", true, false, "Furniture", true, Furniture,
            new StatChange(-12, 50, 38), 100, ActionId, Func, 0);

        measurer.Play(new UseStart(ActionId, 1, Func, false, 0));
        measurer.Cost(call);
        measurer.End(ActionId, false, 60);
        measurer.TakeChanged();
    }
}
