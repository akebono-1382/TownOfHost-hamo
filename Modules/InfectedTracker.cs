using System.Collections.Generic;

using TownOfHost.Roles.Core;

namespace TownOfHost.Modules;

/// <summary>
/// インフェクト/インフェクデッドの「感染」状態を管理する共通トラッカー。
/// 対象は自身が感染していることに気づかない設計のため、通知は一切行わない。
/// 設定ターン数が経過すると、会議開始時にインフェクデッドへ役職を上書きする。
/// </summary>
public static class InfectedTracker
{
    // playerId -> 変化までの残りターン数
    private static readonly Dictionary<byte, int> infected = new();

    public static void Infect(byte playerId, int turnsToTransform)
    {
        // 既に感染済みなら上書きしない(先に発動する方を優先する)。
        if (infected.ContainsKey(playerId)) return;
        infected[playerId] = turnsToTransform;
    }

    /// <summary>会議開始時に呼ぶ。ターンを進め、0になった対象をインフェクデッドへ変化させる。</summary>
    public static void TickAndTransform()
    {
        if (infected.Count == 0) return;

        var toTransform = new List<byte>();
        var keys = new List<byte>(infected.Keys);
        foreach (var id in keys)
        {
            var remaining = infected[id] - 1;
            if (remaining <= 0)
            {
                toTransform.Add(id);
                infected.Remove(id);
            }
            else
            {
                infected[id] = remaining;
            }
        }

        foreach (var id in toTransform)
        {
            var pc = id.GetPlayerControl();
            if (pc == null || !pc.IsAlive()) continue;
            pc.RpcSetCustomRole(CustomRoles.Infected);
        }
    }

    /// <summary>ゲーム開始時に呼ぶ。前回のゲームの感染記録が残らないようにする。</summary>
    public static void Reset() => infected.Clear();
}
