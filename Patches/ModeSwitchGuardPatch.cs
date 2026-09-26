using HarmonyLib;
using TownOfHost.Modules;

namespace TownOfHost;

/// <summary>
/// 起動中のMODモードと異なる操作を防ぎ、プレイ画面の切替ボタンへ誘導する。
/// </summary>
[HarmonyPatch]
public static class ModeSwitchGuardPatch
{
    [HarmonyPatch(typeof(EnterCodeManager), nameof(EnterCodeManager.ClickJoin))]
    [HarmonyPrefix]
    public static bool EnterCodeManagerClickJoinPrefix()
    {
        if (!ModeSwitchManager.IsHostMode) return true;

        ModeSwitchManager.ShowWarning(
            "現在はホスト専用MODです。\n\n" +
            "他の部屋へ参加する前に、プレイ画面上の『参加者専用MOD』を押して切り替えてください。");
        return false;
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenCreateGame))]
    [HarmonyPrefix]
    public static bool MainMenuManagerOpenCreateGamePrefix()
    {
        if (ModeSwitchManager.IsHostMode) return true;

        ModeSwitchManager.ShowWarning(
            "現在は参加者専用MODです。\n\n" +
            "部屋を作成する前に、プレイ画面上の『ホスト専用MOD』を押して切り替えてください。");
        return false;
    }
}
