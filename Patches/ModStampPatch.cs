using System;

using HarmonyLib;

namespace TownOfHost.Patches;

/// <summary>
/// 公式MOD方針で求められるMODスタンプを、ModManagerの初期化後に表示する。
/// スタンプの標準UIはAmong Us側のModManagerに任せる。
/// </summary>
// Among Us V11ではModManager.Awakeが存在しないため、HarmonyPatchとして登録しない。
// このパッチを登録するとPatchAll全体がHarmonyExceptionで中断し、設定UIを含む後続パッチが不安定になる。
// MODスタンプはゲーム側の標準表示へ委ねる。
public static class ModStampPatch
{
    public static void ShowRequiredModStamp()
    {
        try
        {
            ModManager.Instance?.ShowModStamp();
        }
        catch (Exception ex)
        {
            Main.Logger?.LogWarning($"MODスタンプの表示に失敗しました: {ex.Message}");
        }
    }
}
