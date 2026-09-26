using System;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

using TownOfHost.Modules;
using TownOfHost.Templates;

namespace TownOfHost.Patches
{
    // ===== BUG専用チャットパネル =====
    // 要望により、Among Us標準のロビー内チャットとは別に、バグ報告チケットとやり取りする
    // 専用のチャットスペースを用意している。指定したチケット1件分の会話を表示・送信する。
    // メインメニュー・ロビーどちらのBUGボタンからも(ウィザードのチケット一覧経由で)開ける。
    //
    // 見た目の仕様:
    //   - 自分(game)のメッセージは右寄せ、Discord側は左寄せで表示し、吹き出し風に見分けやすくする。
    //   - Enterキーでも送信できる(TextBoxTMP.OnEnterを利用)。
    //   - 自分が入力中の間だけ「入力中…」の表示を出す(自分視点のみ・送信 or フォーカスが外れたら消える)。
    //   - 「更新」ボタンで新着メッセージを手動取得できる。
    public static class BugChatPanel
    {
        private static bool _isOpen = false;
        private static string _currentTicketId = "";
        private static GameObject _panelRoot;
        private static TextMeshPro _historyText;
        private static TextMeshPro _typingIndicatorText;
        private static SimpleTextBox _inputBox;
        private static SimpleButton _sendButton;
        private static SimpleButton _backButton;
        private static SimpleButton _reloadButton;

        private static float _pollTimer = 0f;
        private const float PollIntervalSeconds = 12f; // 要望により短め(10〜15秒)のポーリング間隔

        // パネルの大きさ。画面中央に表示するようになったため、以前より少し大きくして見やすくした。
        private static readonly Vector2 PanelSize = new(5.0f, 4.6f);

        public static void Open(Transform parent, string ticketId)
        {
            // 要望により、バグ報告フォームが開いていたら専用チャットを開いたタイミングで閉じる。
            BugReportWizard.Close();

            if (!EnsurePanel(parent)) return;

            // 要望により、画面のどこでも常に中央に表示されるようにする。
            var camera = Camera.main;
            if (camera != null && parent != null)
            {
                var worldPos = AspectPosition.ComputeWorldPosition(camera, AspectPosition.EdgeAlignments.Center, Vector3.zero);
                worldPos.z = camera.transform.position.z + 5f;
                var centerLocalPosition = parent.InverseTransformPoint(worldPos);
                centerLocalPosition.z = -20f;
                _panelRoot.transform.localPosition = centerLocalPosition;
            }

            _currentTicketId = ticketId;
            BugReportSystem.MarkAsRead(ticketId); // 開いたら未読バッジを消す

            _isOpen = true;
            _panelRoot.SetActive(true);
            SetTypingIndicator(false);
            RefreshHistory();
            _pollTimer = PollIntervalSeconds; // 開いた瞬間にも1回取得する
        }

        public static void Close()
        {
            _isOpen = false;
            SetTypingIndicator(false);
            if (_panelRoot != null && !_panelRoot.IsDestroyedOrNull())
                _panelRoot.SetActive(false);
        }

        /// <summary>指定チケットのチャットが今まさに表示中なら閉じる(修正完了で削除された時などに使う)。</summary>
        public static void CloseIfShowing(string ticketId)
        {
            if (_isOpen && _currentTicketId == ticketId)
            {
                Close();
            }
        }

        /// <summary>ロビー/メインメニューにいる間、パネルが開いていれば定期的にDiscord側の新着を取得する。</summary>
        public static void UpdateTick(float deltaTime)
        {
            if (!_isOpen) return;

            _pollTimer += deltaTime;
            if (_pollTimer < PollIntervalSeconds) return;
            _pollTimer = 0f;

            _ = PollAndRefreshAsync();
        }

        private static async System.Threading.Tasks.Task PollAndRefreshAsync()
        {
            await BugReportSystem.PollAllTicketsAsync().ConfigureAwait(false);
            BugReportSystem.MarkAsRead(_currentTicketId); // 表示中のチケットの未読はすぐ消す
            RefreshHistory();
        }

        /// <summary>「更新」ボタン用: 手動で今すぐ新着メッセージを取得する。</summary>
        private static void ManualReload()
        {
            _pollTimer = 0f; // 自動ポーリングのタイマーもリセットしておく
            _ = PollAndRefreshAsync();
        }

        // 入力中表示は廃止。
        private static void SetTypingIndicator(bool visible) { }

        private static void RefreshHistory()
        {
            if (_historyText == null) return;

            var ticket = BugReportSystem.GetTicket(_currentTicketId);
            if (ticket == null)
            {
                _historyText.text = "<color=#aaaaaa>このチケットは見つかりませんでした。</color>";
                _inputBox?.SetActive(false);
                _sendButton?.Button.gameObject.SetActive(false);
                return;
            }

            _inputBox?.SetActive(true);
            _sendButton?.Button.gameObject.SetActive(true);

            var sb = new StringBuilder();
            var recent = ticket.Messages.TakeLast(10).ToList();
            if (recent.Count == 0)
            {
                sb.Append("<color=#aaaaaa>まだメッセージはありません。</color>");
            }
            else
            {
                foreach (var msg in recent)
                {
                    // 修正完了などのシステムメッセージは、専用チャット内では短く表示する。
                    if (msg.Resolved)
                    {
                        sb.Append("<align=center><color=#4caf50>― 修正完了 ―</color></align>\n");
                        continue;
                    }

                    var isDiscord = msg.From == "discord";
                    // 要望により、自分(game)は右寄せ、Discord側は左寄せにして見分けやすくする。
                    if (isDiscord)
                    {
                        sb.Append($"<align=left><color=#7289da>{msg.Author}</color>\n{msg.Text}</align>\n\n");
                    }
                    else
                    {
                        sb.Append($"<align=right><color=#8cffff>{msg.Author}</color>\n{msg.Text}</align>\n\n");
                    }
                }
            }
            _historyText.text = sb.ToString();
        }

        private static bool EnsurePanel(Transform parent)
        {
            if (_panelRoot != null && !_panelRoot.IsDestroyedOrNull())
            {
                _panelRoot.transform.SetParent(parent, false);
                return true;
            }

            try
            {
                _panelRoot = new GameObject("BugChatPanel");
                _panelRoot.transform.SetParent(parent, false);
                _panelRoot.transform.localPosition = new Vector3(0f, 0f, -20f);

                var bgTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                bgTexture.SetPixel(0, 0, Color.white);
                bgTexture.Apply();
                var bgSprite = Sprite.Create(bgTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

                var bgRenderer = _panelRoot.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = bgSprite;
                bgRenderer.drawMode = SpriteDrawMode.Sliced;
                bgRenderer.size = PanelSize;
                bgRenderer.color = new Color(0f, 0f, 0f, 0.9f);
                bgRenderer.sortingOrder = 950; // 要望により最前面に来るよう大きめの値にしている

                // 要望により、こちらのパネルにもピンクの縁を付ける(BugReportUIPatchと同じ処理を流用)。
                BugReportUIPatch.CreatePinkBorder(_panelRoot.transform, PanelSize, 949);

                // ↓ここから、パネルの上端・下端(半分の高さ = 2.3)を超えないように配置している。

                var title = TMPTemplate.Create(
                    name: "BugChatTitle",
                    text: "BUG専用チャット",
                    color: Color.white,
                    fontSize: 1.9f,
                    alignment: TextAlignmentOptions.Top,
                    setActive: true,
                    parent: _panelRoot.transform);
                SetTmpPosition(title, new Vector3(-0.3f, 2.0f, -1f), new Vector2(3.4f, 0.4f));
                SetSortingOrder(title, 960);

                var closeButton = new SimpleButton(
                    parent: _panelRoot.transform,
                    name: "BugChatCloseButton",
                    localPosition: new Vector3(2.15f, 2.0f, -1f),
                    normalColor: new Color32(120, 30, 30, 255),
                    hoverColor: new Color32(160, 40, 40, 255),
                    action: () => Close(),
                    label: "X");
                closeButton.Scale = new Vector2(0.4f, 0.4f);
                closeButton.FontSize = 1.4f;

                _reloadButton = new SimpleButton(
                    parent: _panelRoot.transform,
                    name: "BugChatReloadButton",
                    localPosition: new Vector3(1.55f, 2.0f, -1f),
                    normalColor: new Color32(60, 90, 150, 230),
                    hoverColor: new Color32(80, 110, 180, 230),
                    action: () => ManualReload(),
                    label: "更新");
                _reloadButton.Scale = new Vector2(0.65f, 0.4f);
                _reloadButton.FontSize = 1.1f;

                _historyText = TMPTemplate.Create(
                    name: "BugChatHistory",
                    text: "",
                    color: Color.white,
                    fontSize: 1.15f,
                    alignment: TextAlignmentOptions.TopLeft,
                    setActive: true,
                    parent: _panelRoot.transform);
                // 上端(タイトル下)〜下端(入力欄上)の範囲に収まるよう高さを調整。
                SetTmpPosition(_historyText, new Vector3(0f, 0.6f, -1f), new Vector2(4.5f, 2.5f));
                _historyText.enableWordWrapping = true; // 要望により、長いメッセージは自動で折り返す
                _historyText.overflowMode = TextOverflowModes.Truncate;
                _historyText.lineSpacing = 4f; // 行間を少し広げて見やすくする
                SetSortingOrder(_historyText, 961);

                _inputBox = new SimpleTextBox(
                    parent: _panelRoot.transform,
                    name: "BugChatInputBox",
                    localPosition: new Vector3(-2.25f, -1.9f, -1f),
                    width: 3.1f,
                    height: 0.7f,
                    characterLimit: 200,
                    placeholder: "メッセージを入力…",
                    multiline: true,
                    onFocusGained: null,
                    onFocusLost: null,
                    onEnter: () => SendCurrentInput()); // 要望により、Enterキーでも送信できるようにする

                _sendButton = new SimpleButton(
                    parent: _panelRoot.transform,
                    name: "BugChatSendButton",
                    localPosition: new Vector3(1.7f, -1.9f, -1f),
                    normalColor: new Color32(60, 150, 60, 230),
                    hoverColor: new Color32(80, 180, 80, 230),
                    action: () => SendCurrentInput(),
                    label: "送信");
                _sendButton.Scale = new Vector2(1.1f, 0.45f);
                _sendButton.FontSize = 1.3f;

                _backButton = new SimpleButton(
                    parent: _panelRoot.transform,
                    name: "BugChatBackButton",
                    localPosition: new Vector3(-1.9f, -2.2f, -1f),
                    normalColor: new Color32(60, 60, 60, 230),
                    hoverColor: new Color32(90, 90, 90, 230),
                    action: () => Close(),
                    label: "戻る");
                _backButton.Scale = new Vector2(1.4f, 0.32f);
                _backButton.FontSize = 1.15f;

                _panelRoot.SetActive(false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"BUGチャットパネルの生成に失敗しました: {ex}", "BugChatPanel");
                if (_panelRoot != null) UnityEngine.Object.Destroy(_panelRoot);
                _panelRoot = null;
                return false;
            }
        }

        /// <summary>
        /// TMPTemplateで複製したTextMeshProの表示位置を確実に指定するヘルパー。
        /// 以前はrectTransform.anchoredPosition3Dを使っていたが、パネルの親にRectTransformが
        /// 無いため、x座標を0以外にした要素だけ計算が不安定になり、想定と全く違う位置
        /// (パネルの外側)に表示される不具合が起きていた。localPositionを使うことで解消する。
        /// </summary>
        private static void SetTmpPosition(TextMeshPro tmp, Vector3 position, Vector2 sizeDelta)
        {
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localPosition = position;
            rt.sizeDelta = sizeDelta;
        }

        private static void SetSortingOrder(TextMeshPro tmp, int order)
        {
            var renderer = tmp.GetComponent<Renderer>();
            if (renderer != null) renderer.sortingOrder = order;
        }

        private static void SendCurrentInput()
        {
            var text = _inputBox?.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputBox.SetText("");
            SetTypingIndicator(false);
            _ = SendAsync(text);
        }

        private static async System.Threading.Tasks.Task SendAsync(string text)
        {
            var playerName = PlayerControl.LocalPlayer != null
                ? PlayerControl.LocalPlayer.GetNameWithRole().RemoveHtmlTags()
                : "プレイヤー";

            var error = await BugReportSystem.ReplyAsync(_currentTicketId, text, playerName).ConfigureAwait(false);
            RefreshHistory();
            // 要望により、送信結果のシステムメッセージはゲーム内チャットには出さない
            // (エラー時のみ、専用チャット内の履歴更新で分かるようにする)。
        }

        // メインメニューが開いている間、パネルが開いていれば定期ポーリングする。
        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
        public static class MainMenuBugChatUpdatePatch
        {
            private static float _mainMenuPollTimer = 0f;
            private const float MainMenuPollIntervalSeconds = 15f;

            public static void Postfix()
            {
                UpdateTick(UnityEngine.Time.deltaTime);

                // チャットパネルが閉じていても、メインメニューにいる間は未読バッジ用に
                // 定期的にチケットの新着を確認しておく。
                _mainMenuPollTimer += UnityEngine.Time.deltaTime;
                if (_mainMenuPollTimer < MainMenuPollIntervalSeconds) return;
                _mainMenuPollTimer = 0f;
                _ = BugReportSystem.PollAllTicketsAsync();
            }
        }
    }
}
