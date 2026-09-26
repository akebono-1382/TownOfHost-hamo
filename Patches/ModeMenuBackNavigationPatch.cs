using HarmonyLib;

namespace TownOfHost;

/// <summary>
/// モード別の直接遷移先から戻る時は、バニラのオンライン画面遷移を通さず、
/// ローカル／オンラインが並ぶゲームモード選択画面へ戻す。
/// </summary>
[HarmonyPatch]
public static class ModeMenuBackNavigationPatch
{
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.GoBackCreateGame))]
    [HarmonyPrefix]
    public static bool GoBackCreateGamePrefix(MainMenuManager __instance)
    {
        if (__instance == null) return true;

        // 元のGoBackCreateGameはオンライン画面を再度開くため、オンライン中間画面省略処理と
        // 衝突してゲーム作成へ戻ってしまう。元処理を実行せず、最初のモード選択画面へ直接戻す。
        __instance.OpenGameModeMenu();
        return false;
    }
}
