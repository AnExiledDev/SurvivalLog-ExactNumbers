using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

/// <summary>page.js against a fake Root.html, answered by the real core, the way the plugin answers it.</summary>
public class PageScriptTests
{
    private const string Minus = "\u2212";
    private const string TwoCostMarks = Minus + Minus;
    private const string Recipes = "WebUI_ToolTable_RecipeMsg";

    private static string Cost(string drawn) => "{k:'cost',v:" + JsText.Literal(drawn) + "}";

    private static string Button(params string[] chips) => "[" + string.Join(",", chips) + "]";

    private static string ButtonList(params string[] previews) =>
        "{\"btnList\":[" + string.Join(",", previews.Select(preview => "{\"attrDelta\":" + JsText.Literal(preview) + "}")) + "]}";

    private static string RecipeList(params string[] previews) =>
        "{\"show\":true,\"recipes\":[" + string.Join(",", previews.Select(preview => "{\"productAttrDelta\":" + JsText.Literal(preview) + "}")) + "]}";

    private static string Raw(string pageId, string name, string json) => string.Join('\u001e', "8", pageId, name, json);

    private static string Estimate(int amount) => ChipLabels.EstimateMark + (amount < 0 ? Minus + -amount : "+" + amount);

    /// <summary>The menu message for this open was passed on and its answer is still in flight.</summary>
    private static PageScript MenuInFlight(string preview, string drawn)
    {
        var page = new PageScript();

        page.GameSends("SelectMenu", GameMessage.MenuMessage, ButtonList(preview));
        page.OpenMenu("SelectMenu", Button(Cost(drawn)));
        page.Poll();

        return page;
    }

    private static PageScript ToolTable(string draws)
    {
        var page = new PageScript();

        page.OpenToolTable(draws);
        page.Poll();

        return page;
    }

    // ===== Object menus =====

    [Fact]
    public void A_menu_drawn_before_the_plugin_read_its_message_keeps_the_games_marks()
    {
        var page = MenuInFlight("Satiety:-12", TwoCostMarks);

        Assert.Equal(TwoCostMarks, page.MenuChip("SelectMenu", 0, 0));
    }

    [Fact]
    public void The_plugins_answer_puts_the_numbers_on_the_menu()
    {
        var page = MenuInFlight("Satiety:-12", TwoCostMarks);

        page.AnswerGameMessages();

        Assert.Equal(Estimate(-12), page.MenuChip("SelectMenu", 0, 0));
        Assert.Empty(page.Notes);
    }

    /// <summary>
    /// The new menu reuses the page id, and its chips can look exactly like the last open's. Until the new list is read,
    /// the old previews must not be matched to the new buttons.
    /// </summary>
    [Fact]
    public void A_reopened_menu_gets_no_numbers_from_the_last_open_while_its_own_message_is_in_flight()
    {
        var page = MenuInFlight("Satiety:-12", TwoCostMarks);

        page.AnswerGameMessages();
        page.GameSends("SelectMenu", GameMessage.MenuMessage, ButtonList("Satiety:-25"));
        page.OpenMenu("SelectMenu", Button(Cost(TwoCostMarks)));
        page.Poll();

        Assert.Equal(TwoCostMarks, page.MenuChip("SelectMenu", 0, 0));

        page.AnswerGameMessages();

        Assert.Equal(Estimate(-25), page.MenuChip("SelectMenu", 0, 0));
    }

    [Fact]
    public void A_reopened_menu_whose_message_cannot_be_read_gets_no_numbers_from_the_last_open()
    {
        var page = MenuInFlight("Satiety:-12", TwoCostMarks);

        page.AnswerGameMessages();
        page.GameSends("SelectMenu", GameMessage.MenuMessage, "{\"btnList\":[");
        page.OpenMenu("SelectMenu", Button(Cost(TwoCostMarks)));
        page.AnswerGameMessages();
        page.Poll();

        Assert.Equal(TwoCostMarks, page.MenuChip("SelectMenu", 0, 0));
    }

    /// <summary>A script reloaded after it passed a message on can be answered for that message; the answer is stale.</summary>
    [Fact]
    public void An_answer_numbered_above_any_message_passed_on_is_ignored()
    {
        var page = new PageScript();
        var stale = new LabelBook().AnswerGame(7, GameMessage.Read(Raw("SelectMenu", GameMessage.MenuMessage, ButtonList("Satiety:-12"))));

        page.Run("window.__slexactnumbers.takeGame(" + stale + ")");
        page.OpenMenu("SelectMenu", Button(Cost(TwoCostMarks)));
        page.Poll();

        Assert.Equal(TwoCostMarks, page.MenuChip("SelectMenu", 0, 0));
    }

    // ===== What is passed on =====

    [Fact]
    public void Only_menu_and_recipe_messages_are_passed_on_to_the_plugin()
    {
        var page = new PageScript();

        page.GameSends("SelectMenu", "WebUI_SelectMenu_PositionMsg", ButtonList("Satiety:-12"));
        page.GameSends("ToolTable", "WebUI_ToolTable_TipsMsg", "{\"attrDelta\":\"Satiety:-12\"}");
        page.GameSends(GameMessage.MenuMessage, "WebUI_SelectMenu_PositionMsg", ButtonList("Satiety:-12"));

        Assert.Empty(page.PassedOn);
    }

    [Fact]
    public void Menu_and_recipe_messages_are_passed_on_whole()
    {
        var page = new PageScript();

        page.GameSends("SelectMenu", GameMessage.MenuMessage, ButtonList("Satiety:-12"));
        page.GameSends("ToolTable", Recipes, RecipeList("Satiety:-12"));

        Assert.Equal(new[] { Raw("SelectMenu", GameMessage.MenuMessage, ButtonList("Satiety:-12")), Raw("ToolTable", Recipes, RecipeList("Satiety:-12")) }, page.PassedOn);
    }

    // ===== Crafting table =====

    [Fact]
    public void A_recipe_list_puts_its_numbers_on_the_page_before_the_first_hover()
    {
        var page = ToolTable("{'Satiety:-12':[" + Cost(TwoCostMarks) + "]}");

        page.GameSends("ToolTable", Recipes, RecipeList("Satiety:-12"));
        page.AnswerGameMessages();
        page.HoverRecipe("Satiety:-12");

        Assert.Equal(Estimate(-12), page.RecipeChip(0, 0));
        Assert.Equal(1, page.RecipeBuilds);
    }

    [Fact]
    public void A_recipe_tooltip_shown_before_its_numbers_is_built_again_once_they_arrive()
    {
        var page = ToolTable("{'Satiety:-12':[" + Cost(TwoCostMarks) + "]}");

        page.HoverRecipe("Satiety:-12");

        Assert.Equal(TwoCostMarks, page.RecipeChip(0, 0));

        page.AnswerNeeds();

        Assert.Equal(Estimate(-12), page.RecipeChip(0, 0));
        Assert.Equal(2, page.RecipeBuilds);
        Assert.Empty(page.Notes);
    }

    /// <summary>Each build is measured and placed by the page, so a tooltip is built again once, not once per preview.</summary>
    [Fact]
    public void A_recipe_tooltip_is_built_again_only_once_every_number_in_it_is_known()
    {
        var page = ToolTable("{'Satiety:-12':[" + Cost(TwoCostMarks) + "],'Stamina:-20':[" + Cost(TwoCostMarks) + "]}");

        page.HoverRecipe("Satiety:-12", "Stamina:-20");
        page.SendLabels("Satiety:-12");

        Assert.Equal(1, page.RecipeBuilds);

        page.SendLabels("Stamina:-20");

        Assert.Equal(2, page.RecipeBuilds);
        Assert.Equal(Estimate(-12), page.RecipeChip(0, 0));
        Assert.Equal(Estimate(-20), page.RecipeChip(1, 0));
    }

    [Fact]
    public void A_tooltip_the_page_reused_for_a_material_is_not_built_as_the_recipe_again()
    {
        var page = ToolTable("{'Satiety:-12':[" + Cost(TwoCostMarks) + "]}");

        page.HoverRecipe("Satiety:-12");
        page.Run("toolTable().showMaterialTooltip()");
        page.SendLabels("Satiety:-12");

        Assert.Equal(1, page.RecipeBuilds);
    }

    [Fact]
    public void A_hidden_recipe_tooltip_is_not_built_again()
    {
        var page = ToolTable("{'Satiety:-12':[" + Cost(TwoCostMarks) + "]}");

        page.HoverRecipe("Satiety:-12");
        page.Run("toolTable().hideTooltip()");
        page.SendLabels("Satiety:-12");

        Assert.Equal(1, page.RecipeBuilds);
    }

    [Fact]
    public void Numbers_for_other_previews_do_not_build_the_recipe_tooltip_again()
    {
        var page = ToolTable("{'Satiety:-12':[" + Cost(TwoCostMarks) + "]}");

        page.SendLabels("Satiety:-12");
        page.HoverRecipe("Satiety:-12");
        page.SendLabels("Stamina:-20");

        Assert.Equal(1, page.RecipeBuilds);
        Assert.Equal(Estimate(-12), page.RecipeChip(0, 0));
    }
}
