using AmongUs.Data;
using HarmonyLib;
using TownOfHost.Modules.ChatManager;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    class ChatControllerUpdatePatch
    {
        public static int CurrentHistorySelection = -1;
        public static bool IsQuickChatOnly;
        public static void Prefix()
        {
            if (AmongUsClient.Instance.AmHost && DataManager.Settings.Multiplayer.ChatMode == InnerNet.QuickChatModes.QuickChatOnly)
            {
                IsQuickChatOnly = true;
                DataManager.Settings.Multiplayer.ChatMode = InnerNet.QuickChatModes.FreeChatOrQuickChat; //コマンドを打つためにホストのみ常時フリーチャット開放
            }
        }
        public static void Postfix(ChatController __instance)
        {
            // バグ報告パネルを開いたままチャット画面が開かれた場合も、要望により
            // 表示の競合を避けるためバグ報告パネル側を自動的に閉じる。
            // (Updateはチャット画面が閉じている間も呼ばれ続けるため、
            //  実際にチャット画面が開いている時だけ判定する)
            if (__instance.IsOpenOrOpening)
                TownOfHost.Patches.BugReportWizard.Close();

            Modules.MatchmakingWordManager.TickChatUiRefresh();
            if (Modules.MatchmakingWordManager.TryHandleEditorHotkeys()) return;
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
                ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
                __instance.freeChatField.textArea.SetText(__instance.freeChatField.textArea.text + GUIUtility.systemCopyBuffer);
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.X))
            {
                ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);
                __instance.freeChatField.textArea.SetText("");
            }
            if (Input.GetKeyDown(KeyCode.UpArrow) && ChatCommands.ChatHistory.Count > 0)
            {
                CurrentHistorySelection = Mathf.Clamp(--CurrentHistorySelection, 0, ChatCommands.ChatHistory.Count - 1);
                __instance.freeChatField.textArea.SetText(ChatCommands.ChatHistory[CurrentHistorySelection]);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && ChatCommands.ChatHistory.Count > 0)
            {
                CurrentHistorySelection++;
                if (CurrentHistorySelection < ChatCommands.ChatHistory.Count)
                    __instance.freeChatField.textArea.SetText(ChatCommands.ChatHistory[CurrentHistorySelection]);
                else __instance.freeChatField.textArea.SetText("");
            }
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.ForceClosed))]
    class ChatControllerForceClosedPatch
    {
        public static bool Prefix()
        {
            return !ChatManager.IsForceSend;
        }
    }
}