using HarmonyLib;

using TownOfHost.Roles.Core;

using TownOfHost.Roles.Crewmate;

using TownOfHost.Roles.Impostor;



namespace TownOfHost

{

    [HarmonyPatch(typeof(GameData), nameof(GameData.RecomputeTaskCounts))]

    class CustomTaskCountsPatch

    {

        public static bool Prefix(GameData __instance)

        {

            __instance.TotalTasks = 0;

            __instance.CompletedTasks = 0;

            foreach (var p in __instance.AllPlayers)

            {

                if (p == null) continue;

                // 決闘者は中立役職で、宿敵撃破による追加勝利を持つため、
                // 通常タスクを村人側の共通ノルマへ加算しない。
                // 除外しないと、決闘者のタスクが残ったまま村人勝利が成立しない。
                if (p._object != null && p._object.Is(CustomRoles.Duelist)) continue;

                var hasTasks = UtilsTask.HasTasks(p) && PlayerState.GetByPlayerId(p.PlayerId).GetTaskState().AllTasksCount > 0;

                if (hasTasks)

                {

                    if (p.Tasks == null)

                    {

                        Logger.Warn("警告:" + p.PlayerName + "のタスクがnullです", "RecompteTaskPatch");

                        continue;//これより下を実行しない

                    }/*

                    foreach (var task in p.Tasks)

                    {

                        __instance.TotalTasks++;

                        if (task.Complete) __instance.CompletedTasks++;

                    }*/

                    {

                        var task = PlayerState.GetByPlayerId(p.PlayerId).GetTaskState();

                        __instance.TotalTasks += task.AllTasksCount;

                        __instance.CompletedTasks += task.CompletedTasksCount;

                    }



                    if (p._object is null) continue;

                    var roleclass = p.Object.GetRoleClass();

                    if (roleclass is Roles.Core.Interfaces.IRoomTasker roomTasker && (roomTasker?.GetMaxTaskCount() is not null))

                    {

                        __instance.TotalTasks += roomTasker.GetMaxTaskCount().Value;

                        __instance.CompletedTasks += roomTasker.GetMyRoomData(p._object.PlayerId).completeroom;

                    }

                }

            }



            return false;

        }

    }

}