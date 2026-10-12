using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

/// <summary>Which furniture function each chip on the page belongs to, and the key the page asks its numbers by.</summary>
public class PageSitesTests
{
    private const char Separator = '\u001e';
    private const int Bed = 500;

    private static GameFacts MenuFacts(params string[] buttons) =>
        GameMessage.Read(string.Join(Separator, "8", "SelectMenu", GameMessage.MenuMessage, "{\"btnList\":[" + string.Join(",", buttons) + "]}"));

    private static string Button(string preview, int? funcId = null) =>
        "{\"attrDelta\":" + JsText.Literal(preview) + (funcId is { } id ? ",\"funcId\":" + id : "") + "}";

    private static GameFacts RecipeFacts(params (string Key, string Preview)[] recipes) =>
        GameMessage.Read(string.Join(Separator, "8", "ToolTable", "WebUI_ToolTable_RecipeMsg",
            "{\"recipes\":[" + string.Join(",", recipes.Select(recipe => "{\"recipeKey\":" + JsText.Literal(recipe.Key) + ",\"productAttrDelta\":" + JsText.Literal(recipe.Preview) + "}")) + "]}"));

    private static MenuState State(params MenuButton[] buttons) => new(Bed, buttons);

    // ===== Label keys =====

    [Fact]
    public void A_chip_with_a_known_function_is_asked_for_by_furniture_function_and_preview()
    {
        var key = LabelKey.For(new MeasureSite(Bed, 301), "Satiety:-12");

        Assert.Equal("@500/301/Satiety:-12", key);
        Assert.Equal(((MeasureSite?)new MeasureSite(Bed, 301), "Satiety:-12"), LabelKey.Parse(key));
    }

    [Fact]
    public void A_chip_without_a_known_function_is_asked_for_by_its_plain_preview()
    {
        Assert.Equal("Satiety:-12", LabelKey.For(null, "Satiety:-12"));
        Assert.Equal("Satiety:-12", LabelKey.For(new MeasureSite(0, 301), "Satiety:-12"));
        Assert.Equal("", LabelKey.For(new MeasureSite(Bed, 301), ""));
        Assert.Equal(((MeasureSite?)null, "Satiety:-12"), LabelKey.Parse("Satiety:-12"));
    }

    [Theory]
    [InlineData("@500/Satiety:-12")]
    [InlineData("@x/301/Satiety:-12")]
    [InlineData("@0/301/Satiety:-12")]
    [InlineData("@500/-301/Satiety:-12")]
    [InlineData("@500/99999999999/Satiety:-12")]
    public void A_key_not_written_by_the_mod_is_read_as_a_plain_preview(string key)
    {
        Assert.Equal(((MeasureSite?)null, key), LabelKey.Parse(key));
    }

    // ===== Object menus =====

    [Fact]
    public void A_function_id_the_menu_message_carries_comes_first()
    {
        var answer = PageSites.Answer(MenuFacts(Button("Satiety:-12", 301)), State(new MenuButton(999, "Satiety:-12")), new RecipeSites());

        Assert.Equal(new[] { "@500/301/Satiety:-12" }, answer.Menu!.Keys);
    }

    [Fact]
    public void Without_one_each_button_takes_the_function_of_the_menu_states_button_at_its_place()
    {
        var answer = PageSites.Answer(
            MenuFacts(Button("Satiety:-12"), Button("Stamina:-20")),
            State(new MenuButton(301, "Satiety:-12"), new MenuButton(302, null)),
            new RecipeSites());

        Assert.Equal(new[] { "@500/301/Satiety:-12", "@500/302/Stamina:-20" }, answer.Menu!.Keys);
        Assert.Equal(new MenuRead(0, null, 301, "@500/301/Satiety:-12"), answer.Reads[0]);
    }

    /// <summary>A state left from another menu must not hand its functions to this one's buttons.</summary>
    [Fact]
    public void A_menu_state_listing_another_number_of_buttons_is_not_used()
    {
        var answer = PageSites.Answer(MenuFacts(Button("Satiety:-12", 301)), State(new MenuButton(301, "Satiety:-12"), new MenuButton(302, "Stamina:-20")), new RecipeSites());

        Assert.Equal(new[] { "Satiety:-12" }, answer.Menu!.Keys);
    }

    [Fact]
    public void A_menu_state_button_showing_another_preview_gives_its_place_no_function()
    {
        var answer = PageSites.Answer(
            MenuFacts(Button("Satiety:-12"), Button("Stamina:-20")),
            State(new MenuButton(301, "Satiety:-12"), new MenuButton(302, "Stamina:-25")),
            new RecipeSites());

        Assert.Equal(new[] { "@500/301/Satiety:-12", "Stamina:-20" }, answer.Menu!.Keys);
    }

    [Fact]
    public void Without_the_menu_state_the_furniture_is_unknown_so_every_button_keeps_its_plain_preview()
    {
        var answer = PageSites.Answer(MenuFacts(Button("Satiety:-12", 301)), null, new RecipeSites());

        Assert.Equal(new[] { "Satiety:-12" }, answer.Menu!.Keys);
        Assert.Equal(new MenuRead(0, 301, null, "Satiety:-12"), Assert.Single(answer.Reads));
    }

    [Fact]
    public void Every_key_on_the_page_is_answered_once_and_a_button_without_a_preview_asks_for_nothing()
    {
        var answer = PageSites.Answer(
            MenuFacts(Button("Satiety:-12"), Button(""), Button("Satiety:-12")),
            State(new MenuButton(301, null), new MenuButton(303, null), new MenuButton(301, null)),
            new RecipeSites());

        Assert.Equal(new[] { "@500/301/Satiety:-12", "", "@500/301/Satiety:-12" }, answer.Menu!.Keys);
        Assert.Equal(new[] { "Satiety:-12", "@500/301/Satiety:-12" }, answer.Keys);
    }

    // ===== Crafting recipes =====

    [Fact]
    public void A_recipe_whose_function_was_noted_is_answered_with_its_key()
    {
        var recipes = new RecipeSites();

        recipes.Note("r1", new RecipeSite(new MeasureSite(600, 40), "Satiety:+30"));

        var answer = PageSites.Answer(RecipeFacts(("r1", "Satiety:+30"), ("r2", "Morale:+2")), null, recipes);

        var mapped = Assert.Single(answer.Recipes);

        Assert.Equal("r1", mapped.Key);
        Assert.Equal("@600/40/Satiety:+30", mapped.Value);
        Assert.Contains("@600/40/Satiety:+30", answer.Keys);
        Assert.Contains("Morale:+2", answer.Keys);
    }

    /// <summary>The game changed what the recipe shows since its function was noted, so that function may not be behind it.</summary>
    [Fact]
    public void A_recipe_showing_another_preview_than_the_one_noted_is_not_mapped()
    {
        var recipes = new RecipeSites();

        recipes.Note("r1", new RecipeSite(new MeasureSite(600, 40), "Satiety:+20"));

        Assert.Empty(PageSites.Answer(RecipeFacts(("r1", "Satiety:+30")), null, recipes).Recipes);
        Assert.Null(recipes.For("r1", "Satiety:+30"));
        Assert.Equal((MeasureSite?)new MeasureSite(600, 40), recipes.For("r1", "Satiety:+20"));
    }

    [Fact]
    public void A_recipe_whose_function_was_forgotten_is_not_mapped()
    {
        var recipes = new RecipeSites();

        recipes.Note("r1", new RecipeSite(new MeasureSite(600, 40), "Satiety:+30"));
        recipes.Forget("r1");

        Assert.Null(recipes.For("r1", "Satiety:+30"));
    }

    [Fact]
    public void The_recipe_map_is_bounded_but_a_known_recipe_can_still_be_renoted()
    {
        var recipes = new RecipeSites();
        var site = new RecipeSite(new MeasureSite(600, 40), "Satiety:+30");

        for (var i = 0; i < RecipeSites.MaxRecipes; i++) Assert.True(recipes.Note("r" + i, site));

        Assert.False(recipes.Note("new", site));
        Assert.True(recipes.Note("r0", site with { Preview = "Satiety:+31" }));
        Assert.Equal(RecipeSites.MaxRecipes, recipes.Count);
        Assert.Null(recipes.For("new", "Satiety:+30"));
    }
}
