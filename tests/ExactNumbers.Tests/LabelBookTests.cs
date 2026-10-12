using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class LabelBookTests
{
    private static Modifiers SatietyCost(float ratio) =>
        new(new Dictionary<RatioKey, float> { [new RatioKey(Stat.Satiety, false)] = ratio });

    [Fact]
    public void An_answer_carries_the_numbers_for_the_preview_asked_about()
    {
        var book = new LabelBook();

        Assert.Equal(
            "{\"Satiety:-12\":[{\"k\":\"cost\",\"v\":\"\u2212\u2212\",\"s\":\"\u221212\",\"b\":null}]}",
            book.Answer("Satiety:-12"));
    }

    [Fact]
    public void Answers_use_the_latest_modifiers()
    {
        var book = new LabelBook();

        book.Update(SatietyCost(0.25f));

        Assert.Contains("\"s\":\"\u221215\",\"b\":\"\u221212\"", book.Answer("Satiety:-12"));
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
        Assert.Contains("\"Satiety:-12\":[{\"k\":\"cost\",\"v\":\"\u2212\u2212\",\"s\":\"\u221218\"", resend);
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
}
