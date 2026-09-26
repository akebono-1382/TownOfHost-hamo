using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using HarmonyLib;
using TMPro;
using InnerNet;

using TownOfHost.Templates;
using TownOfHost.Modules.ClientOptions;

namespace TownOfHost
{
    [HarmonyPatch(typeof(EnterCodeManager))]
    class EnterCodeManagerPatch
    {
        private static GameObject ModScreen;
        private static PassiveButton RestoreButton;
        
        private static PassiveButton UnloadAndJoinButton;
        private static TextMeshPro VersionText;
        private static PassiveButton ServerList;
        private static ServerDropdown ServerListDrop;
        public static Checkbox vServerOnlyCheckBox;
        public static (IRegionInfo region, string code)? lastGameInfo = null;

        [HarmonyPatch(nameof(EnterCodeManager.ClickJoin)), HarmonyPrefix]
        public static void ClickJoinPrefix(EnterCodeManager __instance)
        {
            if (__instance.enterCodeField == null) return;
            var region = DestroyableSingleton<ServerManager>.Instance.CurrentRegion;
            SetLastGameInfo(__instance.enterCodeField.text, region);
        }

        [HarmonyPatch(nameof(EnterCodeManager.OnEnable)), HarmonyPostfix]
        public static void OnEnableBackButtonPostfix(EnterCodeManager __instance)
        {
            // V11に対応した実在のBackButtonへクリック処理を直接設定する。
            // コード入力画面からはローカル／オンライン画面へ戻る。
            if (__instance?.BackButton == null) return;

            // V11のBackButtonはUiElement型のため、実際のクリックコンポーネントを取得する。
            var backButton = __instance.BackButton.GetComponent<PassiveButton>();
            if (backButton == null) return;

            backButton.OnClick = new();
            backButton.OnClick.AddListener((Action)(() =>
            {
                var mainMenu = UnityEngine.Object.FindObjectOfType<MainMenuManager>();
                if (mainMenu != null)
                    mainMenu.OpenGameModeMenu();
            }));
        }

        [HarmonyPatch(nameof(EnterCodeManager.OnEnable)), HarmonyPrefix]
        public static void OnEnablePrefix(EnterCodeManager __instance)
        {
            if (__instance == null || __instance.joinGamePassiveButton == null) return;

            TextMeshPro[] serverTexts = [];

            if (ModScreen.IsDestroyedOrNull())
            {
                var joinGameButton = __instance.joinGamePassiveButton;
                ModScreen = new GameObject("Mod_Screen");
                ModScreen.transform.SetParent(joinGameButton.transform.parent);
                ModScreen.transform.localPosition = Vector3.zero;
            }

            if (RestoreButton.IsDestroyedOrNull())
            {
                var joinGameButton = __instance.joinGamePassiveButton;
                var button = UnityEngine.Object.Instantiate(joinGameButton, ModScreen.transform);
                var text = button.buttonText;

                button.name = "Restore";
                button.transform.localPosition += new Vector3(2.5f, 0f);
                button.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

                text.DestroyTranslator();
                text.text = TryGetString("RestorCode", "コード復元");

                button.OnClick = new();
                button.OnClick.AddListener((Action)(() =>
                {
                    if (!lastGameInfo.HasValue) return;

                    var (region, code) = lastGameInfo.Value;
                    __instance.enterCodeField.SetText(code);
                    __instance.enterCodeField.placeholderText.gameObject.SetActive(false);
                    DestroyableSingleton<ServerManager>.Instance.SetRegion(region);
                    SetCurrentServer();
                }));
                RestoreButton = button;
            }

            

            if (UnloadAndJoinButton.IsDestroyedOrNull())
            {
                var joinGameButton = __instance.joinGamePassiveButton;
                var button = UnityEngine.Object.Instantiate(joinGameButton, ModScreen.transform);
                var text = button.buttonText;

                button.name = "UnloadAndJoin";
                button.transform.localPosition += new Vector3(2.5f, 0.5f);
                button.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

                text.DestroyTranslator();
                text.text = TryGetString("UnloadMod", "MOD解除");

                button.OnClick = new();
                button.OnClick.AddListener((Action)(() =>
                {
                    ModUnloaderScreen.Unload();
                    joinGameButton.SetButtonEnableState(true);
                }));
                UnloadAndJoinButton = button;
            }

            if (VersionText.IsDestroyedOrNull())
            {
                VersionText = TMPTemplate.Create(
                    name: "HostVersionText",
                    alignment: TextAlignmentOptions.Center,
                    setActive: true,
                    parent: __instance.joinGamePassiveButton.transform.parent
                );

                VersionText.transform.localPosition += new Vector3(0, -1.62f, -5);
            }

            if (ServerList.IsDestroyedOrNull())
            {
                var moto = GameObject.Find("MainMenuManager/MainUI/AspectScaler/CreateGameScreen/ParentContent/Content/GeneralTab/ServerOption/ServerBox");
                ServerList = UnityEngine.Object.Instantiate(moto, __instance.joinGamePassiveButton.transform.parent).GetComponent<PassiveButton>();
                ServerList.name = "serverList";
                ServerList.OnClick = new();
                ServerList.OnClick.AddListener(new Action(() =>
                {
                    ServerListDrop?.gameObject?.SetActive(true);
                }));
                ServerList.transform.localPosition = new(-3f, -1.4f, -6f);
                ServerList.transform.localScale = new(0.5f, 0.5f, 1);
            }

            if (ServerListDrop.IsDestroyedOrNull())
            {
                var moto = ObjectHelper.FindObjectsOfTypeAll<ServerDropdown>();
                ServerListDrop = UnityEngine.Object.Instantiate(moto, __instance.joinGamePassiveButton.transform.parent);
                ServerListDrop?.gameObject.SetActive(false);
                ServerListDrop.transform.localPosition = new(-2.0927f, 0.0991f, -15);
                ServerListDrop.transform.localScale = new(0.4f, 0.4f, 1);
            }

            if (vServerOnlyCheckBox.IsDestroyedOrNull())
            {
                vServerOnlyCheckBox = UnityEngine.Object.Instantiate(ObjectHelper.FindObjectsOfTypeAll<Checkbox>(), __instance.joinGamePassiveButton.transform.parent);
                vServerOnlyCheckBox.transform.localPosition = new(-3.17f, -1.05f, -6f);
                vServerOnlyCheckBox.transform.localScale = new(0.75f, 0.75f, 1);
                var boxText = new GameObject("Text_TMP").AddComponent<TextMeshPro>();
                boxText.transform.SetParent(vServerOnlyCheckBox.transform, false);
                boxText.transform.localPosition = new(10.25f, 0f, 0f);

                boxText.alignment = TextAlignmentOptions.Left;
                boxText.fontSize = 1.5f;
            }
            vServerOnlyCheckBox.transform.GetComponentInChildren<TextMeshPro>().text = "選択中のサーバーのみ検索する";

            VersionText.text = "";
            
            UnloadAndJoinButton.SetButtonEnableState(false);
            CheckRestoreButton();

            serverTexts = [
                ServerList.transform.FindChild("Inactive/ClassicText")?.GetComponent<TextMeshPro>(),
                ServerList.transform.FindChild("Highlight/ClassicText")?.GetComponent<TextMeshPro>(),
            ];

            SetCurrentServer();
            ServerListDrop.Initialize((Il2CppSystem.Action<string>)UpdateServerText, (Action)(() => { }));

            void UpdateServerText(string text) => serverTexts.Do(x => x.text = text);

            void SetCurrentServer()
            {
                IRegionInfo currentRegion = DestroyableSingleton<ServerManager>.Instance.CurrentRegion;
                UpdateServerText(DestroyableSingleton<TranslationController>.Instance.GetStringWithDefault(currentRegion.TranslateName, currentRegion.Name));
            }
        }

        [HarmonyPatch(nameof(EnterCodeManager.FindGameResult)), HarmonyPostfix]
        public static void FindGameResultPostfix(EnterCodeManager __instance)
        {
            if (__instance.enterCodeField != null)
                SetLastGameInfo(__instance.enterCodeField.text, DestroyableSingleton<ServerManager>.Instance.CurrentRegion);
            CheckRestoreButton();

            if (VersionText == null) return;

            VersionText.text = "";
            VersionText.fontSize =
            VersionText.fontSizeMin = 1.8f;
            VersionText.color = Color.red;
            
            UnloadAndJoinButton?.SetButtonEnableState(true);

            var hostVersion = CheckHostVersion(__instance?.gameFound);

            if (hostVersion != null)
            {
                if (!MatchVersions(hostVersion))
                {
                    VersionText.fontSize =
                    VersionText.fontSizeMin = 1f;
                    VersionText.text = $"{string.Format(TryGetString("Warning.MismatchedHostVersion", "ホストMODのバージョンが異なります: {0}"), $"<color={Main.ModColor}>{hostVersion.forkId}v{hostVersion.version}</color>")}";
                    
                }
            }
            else
            {
                VersionText.text = TryGetString("Warning.NoModHost", "この部屋のホストはTOH-hamoを導入していません。");
                __instance.joinGamePassiveButton.SetButtonEnableState(false);
            }
        }

        /// <summary>
        /// EnterCodeManagerはTranslationControllerより先に有効化されることがあるため、
        /// 画面生成を止めない固定文言フォールバックを使用する。
        /// </summary>
        private static string TryGetString(string key, string fallback)
        {
            try
            {
                var value = Translator.GetString(key);
                return string.IsNullOrEmpty(value) || value.StartsWith("<INVALID:") ? fallback : value;
            }
            catch (Exception ex)
            {
                Logger.Warn($"翻訳の取得に失敗しました ({key}): {ex.Message}", "EnterCodeManagerPatch");
                return fallback;
            }
        }

        private static void CheckRestoreButton()
            => RestoreButton?.SetButtonEnableState(lastGameInfo.HasValue);
        private static void SetLastGameInfo(string id, IRegionInfo info)
            => lastGameInfo = id.IsNullOrWhiteSpace() || info == null ? lastGameInfo : (info, id);
        public static bool MatchVersions(PlayerVersion version)
            => Main.ForkId == version.forkId
            && Main.version.CompareTo(version.version) == 0;
        public static PlayerVersion CheckHostVersion(GameListing gameFound)
        {
            if (gameFound == null || gameFound.HostPlatformName == null) return null;
            var text = gameFound.HostPlatformName;

            try
            {
                var matches = text.Split("%$");

                string id = matches[1];
                string ver = matches[2];
                return new PlayerVersion(ver, "", id);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex}", "EnterCodeCheckHostver");
                return null;
            }
        }
        public static string VersionTag => $"<size=0>%${Main.ForkId}%${Main.version}%$</size>";
    }

    [HarmonyPatch(typeof(PlatformSpecificData), nameof(PlatformSpecificData.Serialize))]
    class PlatformSpecificDataSerializePatch
    {
        public static string backupPlatformName = "";
        private static void Prefix(PlatformSpecificData __instance)
        {
            // if (!AmongUsClient.Instance.AmHost) return;
            backupPlatformName = __instance.PlatformName;
            __instance.PlatformName += EnterCodeManagerPatch.VersionTag;
        }
        private static void Postfix(PlatformSpecificData __instance)
        {
            //if (!AmongUsClient.Instance.AmHost) return;
            __instance.PlatformName = backupPlatformName;
        }
    }
}