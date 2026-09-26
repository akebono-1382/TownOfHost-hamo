using Epic.OnlineServices.Stats;
using UnityEngine;

namespace TownOfHost
{
    public static class DebugModeManager
    {
        // これが有効の時、通常のゲームに支障のないデバッグ機能(詳細ログ・ゲーム外でのデバッグ表示など)が有効化される。
        // また、ゲーム内オプションでデバッグモードを有効化することができる。
        // 要望により、正式版(Release)では使えないよう、Debug構成でビルドした時だけtrueにする。
        public static bool AmDebugger { get; private set; } =
#if DEBUG
    true;
#else
    false;
#endif
        // これが有効の時、通常のゲームを破壊する可能性のある強力なデバッグ機能(テレポートなど)が有効化される。
        public static bool IsDebugMode => AmDebugger && EnableDebugMode != null && EnableDebugMode.GetBool();

        public static OptionItem EnableDebugMode;
        public static OptionItem EnableTOHhmDebugMode;
        public static OptionItem Spawndummy;
        public static OptionItem DummyAssignRole;

        public static void Auth(HashAuth auth, string input)
        {
            // AmDebugger = デバッグビルドである || デバッグキー認証が通った
            AmDebugger = AmDebugger || auth.CheckString(input);
        }
        public static bool AuthBool(HashAuth auth, string input)
        {
            return auth.CheckString(input);
        }
        public static void SetupCustomOption()
        {
            // 要望により、デバッグ用オプションはメイン設定タブ(1ページ目)ではなく
            // 「Other」タブ(設定の2ページ目)にまとめて表示する。表示条件(SetHidden)は元通り。
            // (AmDebugger自体はmain.cs側のDEBUGビルド設定で切り替えられるため、
            //  それとは別にここで常時表示にする必要はない、とのこと)
            EnableDebugMode = BooleanOptionItem.Create(2, "EnableDebugMode", false, TabGroup.Other, true)
                .SetColor(Color.green)
                .SetHidden(!AmDebugger);
            /*.RegisterUpdateValueEvent((obj, args) =>
            {
                if (DestroyableSingleton<GameStartManager>.InstanceExists && Main.NormalOptions.NumImpostors == 0 && AmongUsClient.Instance.AmHost && !EnableDebugMode.GetBool())
                {
                    Main.NormalOptions.NumImpostors = 1;
                }
            });*/
            // 要望により、デフォルトでオンにしておく。
            EnableTOHhmDebugMode = BooleanOptionItem.Create(3, "EnableTOHhmDebugMode", true, TabGroup.Other, true)
                .SetColor(Color.green)
                .SetHidden(!AmDebugger);
            // Spawndummyが親(EnableTOHhmDebugMode)に紐付いていなかった(マージ時の抜け)のは
            // 素直なバグなので、そこだけは直しておく。
            Spawndummy = IntegerOptionItem.Create(5, "Spawndummy", new(0, 14, 1), 0, TabGroup.Other, true)
                .SetColor(Color.green)
                .SetZeroNotation(OptionZeroNotation.Off)
                .SetHidden(!AmDebugger)
                .SetParent(EnableTOHhmDebugMode);
            DummyAssignRole = BooleanOptionItem.Create(6, "DummyAssignRole", false, TabGroup.Other, true)
                .SetHidden(!AmDebugger)
                .SetParent(Spawndummy);
        }
    }
}