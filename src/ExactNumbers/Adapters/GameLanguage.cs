using System;
using GameCore.HotUpdate;

namespace ExactNumbers.Adapters;

/// <summary>The language the game is set to, read fresh each time.</summary>
internal static class GameLanguage
{
    /// <summary>Whether the game is in Simplified Chinese. Total: a failure, or no settings yet, reads as English.</summary>
    public static bool IsChinese()
    {
        try
        {
            return GameSingleton.Existing<SettingManager>()?.IsChinese() == true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
