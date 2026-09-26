using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;

namespace TownOfHost;

/// <summary>鬼ごっこと牢屋を組み合わせたケイドロモード。</summary>
public static class Kedoro
{
    private static readonly HashSet<byte> Oni = new();
    private static readonly HashSet<byte> Runners = new();
    private static readonly HashSet<byte> Captured = new();
    private static readonly Dictionary<byte, float> LastRescue = new();
    private static readonly List<Vector2> JailPositions = new();
    private static bool SabotageTimerStarted;
    private static float SabotageElapsed;

    public static bool IsActive => Options.IsKedoro;
    public static bool IsOni(PlayerControl pc) => pc != null && Oni.Contains(pc.PlayerId);
    public static bool IsRunner(PlayerControl pc) => pc != null && Runners.Contains(pc.PlayerId);
    public static bool IsCaptured(PlayerControl pc) => pc != null && Captured.Contains(pc.PlayerId);

    public static void Reset()
    {
        Oni.Clear();
        Runners.Clear();
        Captured.Clear();
        LastRescue.Clear();
        JailPositions.Clear();
        SabotageTimerStarted = false;
        SabotageElapsed = 0f;
    }

    /// <summary>Vanillaのインポスターを鬼、その他を逃走者として再分類します。</summary>
    public static void AssignTeams()
    {
        Reset();
        if (AmongUsClient.Instance?.AmHost == true)
            Main.NormalOptions.KillCooldown = Options.KedoroKillCooldown.GetFloat();
        var players = PlayerCatch.AllPlayerControls.Where(pc => pc != null && !pc.IsTestBot() && !pc.Is(CustomRoles.GM)).ToList();
        foreach (var pc in players)
        {
            if (pc.Data?.Role?.Role == RoleTypes.Impostor) Oni.Add(pc.PlayerId);
            else Runners.Add(pc.PlayerId);
        }

        // 逃走者をエンジニア置き換えにする。鬼の人数は通常のインポスター設定を尊重する。
        foreach (var pc in players)
        {
            if (IsOni(pc))
            {
                pc.RpcSetCustomRole(CustomRoles.Impostor, false);
                pc.RpcSetRole(RoleTypes.Impostor, false);
            }
            else
            {
                pc.RpcSetCustomRole(CustomRoles.Engineer, false);
                pc.RpcSetRole(RoleTypes.Engineer, false);

            }
        }

        BuildJailPositions();
    }

    public static void FixedUpdate()
    {
        if (!IsActive || SabotageTimerStarted || GameStates.Intro || GameStates.IsMeeting) return;
        if (AmongUsClient.Instance?.AmHost != true) return;
        SabotageElapsed += Time.fixedDeltaTime;
        if (SabotageElapsed >= Options.KedoroSabotageDelay.GetFloat()) StartSabotageTimer();
    }

    /// <summary>サドンデスと同じく、サボタージュのカウントダウンを残り時間として開始します。</summary>
    private static void StartSabotageTimer()
    {
        if (SabotageTimerStarted || AmongUsClient.Instance?.AmHost != true || ShipStatus.Instance == null) return;
        SabotageTimerStarted = true;
        Main.IsActiveSabotage = true;
        ShipStatus.Instance.RpcUpdateSystem(Utils.GetCriticalSabotageSystemType(), 128);
        UtilsNotifyRoles.NotifyRoles(NoCache: true);
    }

    private static void BuildJailPositions()
    {
        // CustomSpawnEditorで設定した現在プリセットのポイントを牢屋候補として再利用する。
        var points = CustomSpawnManager.GetPoints((MapNames)Main.NormalOptions.MapId);
        if (points != null)
            JailPositions.AddRange(points.Where(p => p != null).Select(p => p.Position));

        // カスタムポイントがない場合は、ランダムスポーンで同期済みの位置を候補にする。
        if (JailPositions.Count == 0)
            JailPositions.AddRange(RandomSpawn.SpawnMap.NextSporn.Values);

        if (JailPositions.Count == 0 && PlayerControl.LocalPlayer != null)
            JailPositions.Add(PlayerControl.LocalPlayer.transform.position);

        var count = Mathf.Clamp(Options.KedoroJailCount.GetInt(), 1, 30);
        if (JailPositions.Count > count)
            JailPositions.RemoveRange(count, JailPositions.Count - count);
    }

    public static void Capture(PlayerControl oni, PlayerControl runner)
    {
        if (!IsActive || !IsOni(oni) || !IsRunner(runner) || IsCaptured(runner) || !runner.IsAlive()) return;
        Captured.Add(runner.PlayerId);
        runner.moveable = false;
        var location = JailPositions.Count == 0
            ? (Vector2)oni.transform.position
            : JailPositions[IRandom.Instance.Next(JailPositions.Count)];
        runner.RpcSnapToForced(location);
        runner.ResetKillCooldown();
        UtilsNotifyRoles.NotifyRoles(NoCache: true);
    }

    private static void RescueNearby(PlayerControl rescuer)
    {
        if (!IsActive || !IsRunner(rescuer) || !rescuer.IsAlive()) return;
        var cooldown = Options.KedoroRescueCooldown.GetFloat();
        if (LastRescue.TryGetValue(rescuer.PlayerId, out var last) && cooldown > 0f && Time.time - last < cooldown) return;

        var target = PlayerCatch.AllPlayerControls
            .Where(pc => IsCaptured(pc) && pc.IsAlive())
            .OrderBy(pc => Vector2.Distance(rescuer.GetTruePosition(), pc.GetTruePosition()))
            .FirstOrDefault(pc => Vector2.Distance(rescuer.GetTruePosition(), pc.GetTruePosition()) <= 1.75f);
        if (target == null) return;

        Captured.Remove(target.PlayerId);
        target.moveable = true;
        target.RpcSnapToForced(rescuer.GetTruePosition() + new Vector2(0.35f, 0f));
        LastRescue[rescuer.PlayerId] = Time.time;
        UtilsNotifyRoles.NotifyRoles(NoCache: true);
    }

    public static void ApplyGameOptions(PlayerControl pc, IGameOptions opt)
    {
        if (!IsActive || !IsRunner(pc)) return;
        opt.SetFloat(FloatOptionNames.EngineerCooldown, Options.KedoroVentCooldown.GetFloat());
        var maxTime = Options.KedoroVentMaxTime.GetFloat();
        opt.SetFloat(FloatOptionNames.EngineerInVentMaxTime, maxTime >= 180f ? 0f : maxTime);
    }

    public static bool CheckGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (!IsActive || CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;

        var aliveRunners = Runners.Count(id => PlayerCatch.GetPlayerById(id)?.IsAlive() == true);
        var capturedRunners = Captured.Count(id => PlayerCatch.GetPlayerById(id)?.IsAlive() == true);
        if (aliveRunners > 0 && capturedRunners >= Runners.Count)
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
            Oni.ToList().ForEach(id => CustomWinnerHolder.WinnerIds.Add(id));
            return true;
        }

        var activeRunners = Runners.Select(PlayerCatch.GetPlayerById).Where(pc => pc != null && pc.IsAlive()).ToList();
        if (activeRunners.Count > 0 && activeRunners.All(pc => pc.AllTasksCompleted()))
        {
            reason = GameOverReason.CrewmatesByTask;
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
            activeRunners.ForEach(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
            return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
    private static class KedoroPetPatch
    {
        public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
        {
            if (!IsActive || (RpcCalls)callID != RpcCalls.Pet) return;
            if (AmongUsClient.Instance?.AmHost != true) return;
            RescueNearby(__instance.myPlayer);
        }
    }

    public sealed class KedoroGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            return CheckGameEnd(out reason);
        }
    }

}
