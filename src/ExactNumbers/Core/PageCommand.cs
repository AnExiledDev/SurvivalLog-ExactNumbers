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

    /// <summary>A need names a label key: a preview, with "@furniture/func/" in front when its function is known.</summary>
    public const int MaxKeyLength = MaxPreviewLength + 32;

    private const string NeedVerb = "need|";
    private const string GameVerb = "game|";

    private PageCommand()
    {
    }

    /// <summary>The page script just installed itself and holds no numbers yet.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>A tooltip drew chips for this label key (see LabelKey) and the page wants their numbers.</summary>
    public sealed record Need(string Key) : PageCommand;

    /// <summary>
    /// A message the game sent one of its pages, passed on by the page script as Root.html received it. Seq is the
    /// script's count of passed-on messages; every one is answered with it, so the script knows when nothing is in flight.
    /// </summary>
    public sealed record Game(int Seq, string Message) : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    /// <remarks>A game message can be large, so only the part kept is copied out of it, once.</remarks>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var body = message.AsSpan(Prefix.Length);

        // The key is the rest of the message, whatever it holds, so it reaches the plugin exactly as the page saw it.
        if (body.StartsWith(NeedVerb, StringComparison.Ordinal))
        {
            var keyLength = body.Length - NeedVerb.Length;

            return keyLength is > 0 and <= MaxKeyLength ? new Need(message.Substring(Prefix.Length + NeedVerb.Length)) : new Unknown(message);
        }

        if (body.StartsWith(GameVerb, StringComparison.Ordinal)) return ParseGame(message, Prefix.Length + GameVerb.Length);

        if (body.Equals("ready", StringComparison.Ordinal)) return new Ready();
        if (body.Equals("noscript", StringComparison.Ordinal)) return new NoScript();

        return new Unknown(message);
    }

    /// <summary>"seq|message" from start on. A bad one is logged by its verb alone, since the message can be very long.</summary>
    private static PageCommand ParseGame(string message, int start)
    {
        var bar = message.IndexOf('|', start);

        if (bar > start && int.TryParse(message.AsSpan(start, bar - start), NumberStyles.None, CultureInfo.InvariantCulture, out var seq)) return new Game(seq, message.Substring(bar + 1));

        return new Unknown(Prefix + GameVerb + " without a message number");
    }
}
