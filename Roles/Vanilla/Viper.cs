using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Vanilla;

public sealed class Viper : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Viper),
            player => new Viper(player),
            RoleTypes.Viper,
            SetUpCustomOption
            , from: From.AmongUs
        );
    public Viper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
                // オプション登録前に役職インスタンスが生成される環境でも起動を止めない。
        ViperDissolveTime = OptViperDissolveTime?.GetFloat() ?? 15f;
        killcool = OptKillcool?.GetFloat() ?? 30f;

        dissolvingPlayers.Clear();
        staticDissolvedPlayers.Clear();
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }
    static OptionItem OptKillcool; static float killcool;
    public static OptionItem OptViperDissolveTime; public static float ViperDissolveTime;
    // 溶解タイマー進行中の死体(PlayerId -> 残り時間)
    readonly Dictionary<byte, float> dissolvingPlayers = new();
    // 溶け終わった死体の一覧(ヴァルチャーのstaticEatedPlayersと同じ考え方で、
    // 会議中に全員から×マークが見えるようにするためのリスト)
    static readonly HashSet<byte> staticDissolvedPlayers = new();
    public static void SetUpCustomOption()
    {
        OptKillcool = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.KillCooldown, OptionBaseCoolTime, 30, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptViperDissolveTime = FloatOptionItem.Create(RoleInfo, 3, StringNames.ViperDissolveTime, new(0, 180, 1), 15, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ViperDissolveTime = ViperDissolveTime;
    }
        // Viperは標準インポスターと同じく通常キルを行えることを明示する。
    bool IKiller.CanUseKillButton() => Player.IsAlive();
    float IKiller.CalculateKillCooldown() => killcool;

    bool IKiller.OverrideKillButtonText(out string text)
    {
        text = GetString(StringNames.ViperAbility);
        return true;
    }

    /// <summary>
    /// キルした瞬間、死体が溶けるまでのタイマーを開始する。
    /// (ヴァルチャーがViperの死体消滅を検知していたのと同じ仕組みを、
    ///  Viper自身の役職側に持たせている)
    /// </summary>
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        if (target == null) return;

        dissolvingPlayers[target.PlayerId] = ViperDissolveTime;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || dissolvingPlayers.Count == 0) return;

        foreach (var (targetId, timer) in dissolvingPlayers.ToArray())
        {
            var newTimer = timer - UnityEngine.Time.fixedDeltaTime;
            if (newTimer <= 0f)
            {
                dissolvingPlayers.Remove(targetId);
                staticDissolvedPlayers.Add(targetId);
            }
            else
            {
                dissolvingPlayers[targetId] = newTimer;
            }
        }
    }

    /// <summary>
    /// 溶けた死体に、会議中ヴァルチャーと同じ×マークを付ける。
    /// </summary>
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting && staticDissolvedPlayers.Contains(seen.PlayerId))
            return $"<{RoleInfo.RoleColorCode}>×</color>";
        return "";
    }

    public override void AfterMeetingTasks()
    {
        staticDissolvedPlayers.Clear();
    }

    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 10, 0, 0);
        achievements.Add(0, n1);
    }
}