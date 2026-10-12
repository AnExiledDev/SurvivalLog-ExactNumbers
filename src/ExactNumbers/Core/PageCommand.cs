using System;

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

    private PageCommand()
    {
    }

    /// <summary>The page script just installed itself and holds no numbers yet.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>A tooltip drew chips for this preview text and the page wants their numbers.</summary>
    public sealed record Need(string Preview) : PageCommand;

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

        return body switch
        {
            "ready" => new Ready(),
            "noscript" => new NoScript(),
            _ => new Unknown(message),
        };
    }
}
