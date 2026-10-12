using System;
using System.Collections.Concurrent;
using System.Threading;
using HarmonyLib;
using ExactNumbers.Core;
using Vuplex.WebView.Internal;

namespace ExactNumbers.Adapters;

/// <summary>
/// Catches the page script's messages before the game's web layer sees them (it would log them as unknown) and queues
/// them for the next frame, so nothing runs on the web view's thread. Static because a Harmony prefix has nowhere else
/// to look.
/// </summary>
internal static class PageMessages
{
    private static readonly ConcurrentQueue<PageCommand> Queue = new();
    private static readonly PageCommand Nothing = new PageCommand.Unknown("");
    private static Action<string> _log = _ => { };
    private static int _hasReportedFailure;

    /// <summary>False when the patch could not be applied: the page script would never be heard, so there is nothing to run.</summary>
    public static bool Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageMessages), nameof(TakeOurs)));
            log("listening for page script messages");

            return true;
        }
        catch (Exception ex)
        {
            log("could not listen for page script messages, Exact Numbers stays off: " + ex.Message);

            return false;
        }
    }

    public static bool TryTake(out PageCommand command)
    {
        if (Queue.TryDequeue(out var queued))
        {
            command = queued;

            return true;
        }

        // Shared, so an empty queue costs no allocation on every frame.
        command = Nothing;

        return false;
    }

    /// <summary>Never throws into the web view: on any failure the message goes on to the game as if the mod were not here.</summary>
    private static bool TakeOurs(string serializedMessage)
    {
        try
        {
            if (!PageCommand.IsOurs(serializedMessage)) return true;

            Queue.Enqueue(PageCommand.Parse(serializedMessage));

            return false;
        }
        catch (Exception ex)
        {
            ReportOnce(ex);

            return true;
        }
    }

    /// <summary>Runs on the web view's thread, so the once-only flag is set atomically.</summary>
    private static void ReportOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _hasReportedFailure, 1) == 0) _log("reading a web UI message failed, it was passed on to the game (reported once): " + ex);
    }
}
