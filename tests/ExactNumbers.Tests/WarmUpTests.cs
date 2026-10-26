using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

/// <summary>
/// The warm-up runs the first menu, measurement and save once at load so the game's first menu does not pay for them.
/// It only helps if it goes all the way through the same code the session runs.
/// </summary>
public class WarmUpTests
{
    [Fact]
    public void The_warm_up_goes_from_a_measured_use_through_a_menu_message_to_the_answer_the_page_would_get()
    {
        var payload = WarmUp.Run();

        Assert.StartsWith("{\"seq\":1,\"menu\":{\"page\":\"SelectMenu\",\"keys\":[\"@500/301/Satiety:-12;MaxStamina:+5\"]}", payload);
        Assert.Contains("\"@500/301/Satiety:-12;MaxStamina:+5\":[{", payload);
        Assert.Contains(Texts.MeasuredOverOneUse.En, payload);
    }

    /// <summary>It runs on a worker beside the game; it must keep nothing between runs and touch nothing the session uses.</summary>
    [Fact]
    public void The_warm_up_keeps_nothing_between_runs()
    {
        Assert.Equal(WarmUp.Run(), WarmUp.Run());
    }
}
