using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class PageCommandTests
{
    [Fact]
    public void Messages_without_the_mod_prefix_are_left_for_the_game()
    {
        Assert.False(PageCommand.IsOurs("slmod|infopack|ready"));
        Assert.False(PageCommand.IsOurs(null));
        Assert.True(PageCommand.IsOurs(PageCommand.Prefix + "ready"));
    }

    [Fact]
    public void Ready_and_noscript_are_read()
    {
        Assert.IsType<PageCommand.Ready>(PageCommand.Parse(PageCommand.Prefix + "ready"));
        Assert.IsType<PageCommand.NoScript>(PageCommand.Parse(PageCommand.Prefix + "noscript"));
    }

    [Fact]
    public void A_request_carries_the_preview_exactly_as_the_page_saw_it()
    {
        var command = PageCommand.Parse(PageCommand.Prefix + "need|Satiety:-12; Stamina:-20|odd");

        Assert.Equal(new PageCommand.Need("Satiety:-12; Stamina:-20|odd"), command);
    }

    [Fact]
    public void An_empty_or_oversized_request_is_not_answered()
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(PageCommand.Prefix + "need|"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(PageCommand.Prefix + "need|" + new string('x', PageCommand.MaxPreviewLength + 1)));
        Assert.IsType<PageCommand.Need>(PageCommand.Parse(PageCommand.Prefix + "need|" + new string('x', PageCommand.MaxPreviewLength)));
    }

    [Fact]
    public void Diagnostic_notes_are_kept_as_unknown_text_for_the_log()
    {
        var message = PageCommand.Prefix + "diag|poll: TypeError";

        Assert.Equal(new PageCommand.Unknown(message), PageCommand.Parse(message));
    }
}
