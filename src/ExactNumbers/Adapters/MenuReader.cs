using System.Collections.Generic;
using ExactNumbers.Core;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace ExactNumbers.Adapters;

/// <summary>
/// Reads the open object menu from the game's menu state: the furniture's config id (State_Web_SelectMenu.SelectConfigId,
/// set from the selected furniture at Reducer_Web_SelectMenu.c:821-830) and each button's function and preview
/// (Data_SelectMenu_Btn.FuncId and AttrDelta). The menu message lists the same buttons in the same order
/// (WebUI_SelectMenu.UpdateBtnList, WebUI_SelectMenu.c:364). It only reads.
/// </summary>
internal static class MenuReader
{
    // The runtime keeps one type object per class for good, so it is looked up once, not on every menu message.
    private static Il2CppSystem.Type? _screen;

    /// <returns>Null when no object menu is open or its state cannot be reached.</returns>
    public static MenuState? Read()
    {
        // ReduxUISystem.IsUIActive (ReduxUISystem.c:1859) and GetStateByUI (843).
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = _screen ??= Il2CppType.Of<WebUI_SelectMenu>();

        if (ui == null || !ui.IsUIActive(screen)) return null;

        var state = ui.GetStateByUI(screen)?.TryCast<State_Web_SelectMenu>();
        var list = state?.BtnList;

        if (state == null || list == null) return null;

        var buttons = new List<MenuButton>();

        for (var i = 0; i < list.Count; i++)
        {
            var button = list[i];

            buttons.Add(new MenuButton(button?.FuncId?.Value ?? 0, button?.AttrDelta?.Value));
        }

        return new MenuState(state.SelectConfigId, buttons);
    }
}
