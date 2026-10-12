using System;
using System.Globalization;

namespace ExactNumbers.Core;

/// <summary>
/// What the page script tells the plugin. It sends "slmod|exactnumbers|" followed by a verb; anything else parses to
/// Unknown so it can be logged rather than acted on (the script's "diag|..." notes land there on purpose).
/// </summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|exactnumbers|";

    /// <summary>Longer than any preview text in the game by far; a longer one is not a preview and is not answered.</summary>
    public const int MaxPreviewLength = 1000;

    private const string NeedVerb = "need|";
    private const string GameVerb = "game|";

    private PageCommand()
    {
    }

    /// <summary>The page script just installed itself and holds no numbers yet.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>A tooltip drew chips for this preview text and the page wants their numbers.</summary>
    public sealed record Need(string Preview) : PageCommand;

    /// <summary>
    /// A message the game sent one of its pages, passed on by the page script as Root.html received it. Seq is the
    /// script's count of passed-on messages; every one is answered with it, so the script knows when nothing is in flight.
    /// </summary>
    public sealed record Game(int Seq, string Message) : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var body = message.Substring(Prefix.Length);

        // The preview is the rest of the message, whatever it holds, so it reaches the plugin exactly as the page saw it.
        if (body.StartsWith(NeedVerb, StringComparison.Ordinal))
        {
            var preview = body.Substring(NeedVerb.Length);

            return preview.Length is > 0 and <= MaxPreviewLength ? new Need(preview) : new Unknown(message);
        }

        if (body.StartsWith(GameVerb, StringComparison.Ordinal)) return ParseGame(body.Substring(GameVerb.Length));

        return body switch
        {
            "ready" => new Ready(),
            "noscript" => new NoScript(),
            _ => new Unknown(message),
        };
    }

    /// <summary>"seq|message". A bad one is logged by its verb alone, since the message can be very long.</summary>
    private static PageCommand ParseGame(string rest)
    {
        var bar = rest.IndexOf('|');

        if (bar > 0 && int.TryParse(rest.AsSpan(0, bar), NumberStyles.None, CultureInfo.InvariantCulture, out var seq)) return new Game(seq, rest.Substring(bar + 1));

        return new Unknown(Prefix + GameVerb + " without a message number");
    }
}
