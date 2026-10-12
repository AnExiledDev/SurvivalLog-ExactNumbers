using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class GameMessageTests
{
    private const char Separator = '\u001e';

    private static string PageMessage(string pageId, string name, string json) => string.Join(Separator, "8", pageId, name, json);

    private static string MenuMessage(string json) => PageMessage("SelectMenu", GameMessage.MenuMessage, json);

    [Fact]
    public void A_menu_lists_each_buttons_preview_in_button_order_so_data_index_finds_it()
    {
        var facts = GameMessage.Read(MenuMessage("{\"btnList\":[{\"attrDelta\":\"Satiety:-12\"},{\"text\":\"Inspect\"},{\"attrDelta\":\"Stamina:-20;Morale:+4\"}]}"));

        Assert.Equal(new[] { "Satiety:-12", "", "Stamina:-20;Morale:+4" }, facts.Menu!.Previews);
    }

    [Fact]
    public void A_menu_belongs_to_the_page_the_game_addressed_it_to()
    {
        var facts = GameMessage.Read(PageMessage("SelectMenu_42", GameMessage.MenuMessage, "{\"btnList\":[]}"));

        Assert.Equal("SelectMenu_42", facts.Menu!.PageId);
    }

    [Fact]
    public void A_button_whose_preview_is_not_text_has_no_preview()
    {
        var facts = GameMessage.Read(MenuMessage("{\"btnList\":[{\"attrDelta\":12},{\"attrDelta\":null},\"odd\"]}"));

        Assert.Equal(new[] { "", "", "" }, facts.Menu!.Previews);
    }

    [Fact]
    public void A_menu_message_without_a_button_list_leaves_the_menu_as_it_was_like_the_page_does()
    {
        Assert.Null(GameMessage.Read(MenuMessage("{\"btnList\":null}")).Menu);
        Assert.Null(GameMessage.Read(MenuMessage("{}")).Menu);
    }

    [Fact]
    public void Only_the_menu_message_makes_a_menu()
    {
        var facts = GameMessage.Read(PageMessage("SelectMenu", "WebUI_SelectMenu_PositionMsg", "{\"btnList\":[{\"attrDelta\":\"Satiety:-12\"}]}"));

        Assert.Null(facts.Menu);
    }

    [Fact]
    public void Previews_anywhere_in_a_page_message_are_collected_so_their_numbers_arrive_before_the_hover()
    {
        var facts = GameMessage.Read(PageMessage("ToolTable", "WebUI_ToolTable_RecipeMsg", "{\"groups\":[{\"recipes\":[{\"productAttrDelta\":\"Satiety:+30\"},{\"productAttrDelta\":\"\"}]}],\"attrDelta\":\"Morale:+2\"}"));

        Assert.Equal(new[] { "Satiety:+30", "Morale:+2" }, facts.Previews);
    }

    [Fact]
    public void A_menus_previews_are_collected_too_each_once()
    {
        var facts = GameMessage.Read(MenuMessage("{\"btnList\":[{\"attrDelta\":\"Satiety:-12\"},{\"attrDelta\":\"Satiety:-12\"},{\"attrDelta\":\"  \"}]}"));

        Assert.Equal(new[] { "Satiety:-12" }, facts.Previews);
    }

    [Fact]
    public void The_number_of_previews_taken_from_one_message_is_bounded()
    {
        var buttons = Enumerable.Range(1, GameMessage.MaxPreviews + 5).Select(i => "{\"attrDelta\":\"Satiety:-" + i + "\"}");

        var facts = GameMessage.Read(MenuMessage("{\"btnList\":[" + string.Join(",", buttons) + "]}"));

        Assert.Equal(GameMessage.MaxPreviews, facts.Previews.Count);
        Assert.Equal(GameMessage.MaxPreviews + 5, facts.Menu!.Previews.Count);
    }

    [Fact]
    public void Messages_that_are_not_for_a_page_teach_nothing()
    {
        Assert.Same(GameFacts.Nothing, GameMessage.Read(string.Join(Separator, "4", "", "", "{\"attrDelta\":\"Satiety:-12\"}")));
    }

    /// <summary>The page may still draw a menu from it; the previous open's previews must not land on those chips.</summary>
    [Fact]
    public void A_menu_message_that_cannot_be_parsed_empties_that_pages_menu()
    {
        var facts = GameMessage.Read(PageMessage("SelectMenu_9", GameMessage.MenuMessage, "{\"btnList\":["));

        Assert.Equal("SelectMenu_9", facts.Menu!.PageId);
        Assert.Empty(facts.Menu!.Previews);
        Assert.Empty(facts.Previews);
    }

    [Fact]
    public void A_menu_message_with_an_escaped_lone_surrogate_empties_that_pages_menu_instead_of_throwing()
    {
        var facts = GameMessage.Read(MenuMessage("{\"btnList\":[{\"attrDelta\":\"Satiety:-12\\ud800\"}]}"));

        Assert.Equal("SelectMenu", facts.Menu!.PageId);
        Assert.Empty(facts.Menu!.Previews);
    }

    [Fact]
    public void A_menu_message_holding_a_raw_lone_surrogate_empties_that_pages_menu_instead_of_throwing()
    {
        var facts = GameMessage.Read(MenuMessage("{\"btnList\":[{\"attrDelta\":\"Satiety:-12\ud800\"}]}"));

        Assert.Empty(facts.Menu!.Previews);
    }

    [Fact]
    public void Another_page_message_with_a_lone_surrogate_teaches_nothing_instead_of_throwing()
    {
        Assert.Same(GameFacts.Nothing, GameMessage.Read(PageMessage("ToolTable", "WebUI_ToolTable_RecipeMsg", "{\"recipes\":[{\"productAttrDelta\":\"\\ud800\"}]}")));
    }

    [Fact]
    public void A_broken_or_oversized_message_teaches_nothing()
    {
        Assert.Same(GameFacts.Nothing, GameMessage.Read(PageMessage("ToolTable", "WebUI_ToolTable_RecipeMsg", "{\"recipes\":[")));
        Assert.Same(GameFacts.Nothing, GameMessage.Read("8" + Separator + "SelectMenu"));
        Assert.Same(GameFacts.Nothing, GameMessage.Read(null));
        Assert.Same(GameFacts.Nothing, GameMessage.Read(MenuMessage("{\"btnList\":[],\"pad\":\"" + new string('x', GameMessage.MaxLength) + "\"}")));
    }
}
