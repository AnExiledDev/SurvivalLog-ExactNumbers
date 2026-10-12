using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class LabelBookTests
{
    private const string Minus = "\u2212";
    private const char Separator = '\u001e';

    private static Modifiers SatietyCost(float ratio) =>
        new(new Dictionary<RatioKey, float> { [new RatioKey(Stat.Satiety, false)] = ratio });

    private static GameFacts Menu(string pageId, params string[] previews) =>
        GameMessage.Read(string.Join(Separator, "8", pageId, GameMessage.MenuMessage, "{\"btnList\":[" + string.Join(",", previews.Select(preview => "{\"attrDelta\":\"" + preview + "\"}")) + "]}"));

    [Fact]
    public void An_answer_carries_the_numbers_for_the_preview_asked_about()
    {
        var book = new LabelBook();

        Assert.Equal(
            "{\"Satiety:-12\":[{\"k\":\"cost\",\"v\":\"" + Minus + Minus + "\",\"s\":\"~" + Minus + "12\",\"t\":" + JsText.Literal(ChipLabels.EstimateTitle) + "}]}",
            book.Answer("Satiety:-12"));
    }

    [Fact]
    public void Answers_use_the_latest_modifiers()
    {
        var book = new LabelBook();

        book.Update(SatietyCost(0.25f));

        Assert.Contains("\"s\":\"~" + Minus + "15\"", book.Answer("Satiety:-12"));
    }

    [Fact]
    public void Unchanged_modifiers_resend_nothing()
    {
        var book = new LabelBook();

        book.Update(SatietyCost(0.25f));
        book.Answer("Satiety:-12");

        Assert.Null(book.Update(SatietyCost(0.25f)));
    }

    [Fact]
    public void Changed_modifiers_resend_every_preview_the_page_asked_about()
    {
        var book = new LabelBook();

        book.Answer("Satiety:-12");
        book.Answer("Stamina:-20");

        var resend = book.Update(SatietyCost(0.5f));

        Assert.NotNull(resend);
        Assert.Contains("\"Satiety:-12\":[{\"k\":\"cost\",\"v\":\"" + Minus + Minus + "\",\"s\":\"~" + Minus + "18\"", resend);
        Assert.Contains("\"Stamina:-20\":", resend);
    }

    [Fact]
    public void A_reloaded_page_has_nothing_to_resend()
    {
        var book = new LabelBook();

        book.Answer("Satiety:-12");
        book.PageReloaded();

        Assert.Null(book.Update(SatietyCost(0.5f)));
    }

    [Fact]
    public void A_preview_that_cannot_be_matched_is_answered_with_null_so_the_page_stops_asking()
    {
        Assert.Equal("{\"Satiety:-99999999999\":null}", new LabelBook().Answer("Satiety:-99999999999"));
    }

    [Fact]
    public void Preview_text_is_escaped_so_it_cannot_break_out_of_the_script()
    {
        var answer = new LabelBook().Answer("x\");alert(1);//</script>");

        Assert.StartsWith("{\"x\\\");alert(1);//\\u003c/script>\":", answer);
    }

    [Fact]
    public void Only_a_bounded_number_of_previews_is_remembered()
    {
        var book = new LabelBook();

        for (var i = 0; i < LabelBook.MaxRemembered + 10; i++) book.Answer("Satiety:-" + (i + 1));

        var resend = book.Update(SatietyCost(0.5f))!;

        Assert.Contains("\"Satiety:-" + LabelBook.MaxRemembered + "\":", resend);
        Assert.DoesNotContain("\"Satiety:-" + (LabelBook.MaxRemembered + 1) + "\":", resend);
    }

    [Fact]
    public void A_menu_answer_gives_the_page_its_button_previews_and_their_numbers_together()
    {
        var answer = new LabelBook().AnswerGame(3, Menu("SelectMenu_7", "Satiety:-12", ""));

        Assert.StartsWith("{\"seq\":3,\"menu\":{\"page\":\"SelectMenu_7\",\"previews\":[\"Satiety:-12\",\"\"]},\"labels\":{\"Satiety:-12\":[{", answer);
    }

    [Fact]
    public void A_message_that_held_nothing_is_still_answered_with_its_number_so_menus_stop_waiting()
    {
        Assert.Equal("{\"seq\":9,\"menu\":null,\"labels\":{}}", new LabelBook().AnswerGame(9, GameFacts.Nothing));
    }

    [Fact]
    public void Previews_sent_with_a_game_message_are_resent_when_the_modifiers_change()
    {
        var book = new LabelBook();

        book.AnswerGame(1, Menu("SelectMenu", "Satiety:-12"));

        Assert.Contains("\"Satiety:-12\":[{\"k\":\"cost\",\"v\":\"" + Minus + Minus + "\",\"s\":\"~" + Minus + "18\"", book.Update(SatietyCost(0.5f)));
    }

    /// <summary>The page keeps every number it was sent, so a recipe list sent again does not cost its labels again.</summary>
    [Fact]
    public void A_game_answer_leaves_out_numbers_the_page_was_already_sent()
    {
        var book = new LabelBook();

        book.AnswerGame(1, Menu("SelectMenu", "Satiety:-12"));
        book.Answer("Stamina:-20");

        var answer = book.AnswerGame(2, Menu("SelectMenu", "Satiety:-12", "Stamina:-20", "Morale:+4"));

        Assert.DoesNotContain("\"Satiety:-12\":", answer);
        Assert.DoesNotContain("\"Stamina:-20\":", answer);
        Assert.Contains("\"Morale:+4\":", answer);
    }

    [Fact]
    public void A_menu_answer_still_lists_every_buttons_preview_when_their_numbers_were_sent_before()
    {
        var book = new LabelBook();

        book.AnswerGame(1, Menu("SelectMenu", "Satiety:-12"));

        Assert.Equal("{\"seq\":2,\"menu\":{\"page\":\"SelectMenu\",\"previews\":[\"Satiety:-12\"]},\"labels\":{}}", book.AnswerGame(2, Menu("SelectMenu", "Satiety:-12")));
    }

    [Fact]
    public void A_reloaded_page_is_sent_every_number_again()
    {
        var book = new LabelBook();

        book.AnswerGame(1, Menu("SelectMenu", "Satiety:-12"));
        book.PageReloaded();

        Assert.Contains("\"Satiety:-12\":[{", book.AnswerGame(1, Menu("SelectMenu", "Satiety:-12")));
    }

    [Fact]
    public void The_page_id_is_escaped_so_it_cannot_break_out_of_the_script()
    {
        var answer = new LabelBook().AnswerGame(1, Menu("x\"</script>", "Satiety:-12"));

        Assert.Contains("\"page\":\"x\\\"\\u003c/script>\"", answer);
    }
}
