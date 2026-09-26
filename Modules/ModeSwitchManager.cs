using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace TownOfHost.Modules;

/// <summary>
/// ホスト版／参加者専用版のゲーム内切替を行う。
/// Pko版のバージョン変更と同じく、実行中にDLLを .bak へ退避してから
/// 選択DLLを plugins へ配置し、Among Usを終了する。外部CMDからゲーム本体を
/// 起動しないため、Doorstop/BepInExを通らず通常Among Usが起動する問題を回避する。
/// </summary>
public static class ModeSwitchManager
{
    private const string HostDllName = "TownOfHost-hamo.dll";
    private const string ClientDllName = "TownOfHost-hamo-Client.dll";
    private static bool isSwitching;

    public static bool IsHostMode => !Main.IsNonHostClient;

    public static void ShowWarning(string message)
    {
        Logger.Warn(message, "ModeSwitch");
        try
        {
            if (ModUpdater.InfoPopup != null)
            {
                ModUpdater.InfoPopup.Show(message);
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "ModeSwitchPopup");
        }
    }

    public static void RequestSwitchAndRestart(bool useHostMode)
    {
        if (isSwitching) return;

        if (useHostMode == IsHostMode)
        {
            ShowWarning(useHostMode
                ? "現在はホスト専用MODです。部屋を作成・開始できます。"
                : "現在は参加者専用MODです。他のTOH-hamo部屋へ参加できます。");
            return;
        }

        try
        {
            var bepinExRoot = Paths.BepInExRootPath;
            var pluginDirectory = Path.Combine(bepinExRoot, "plugins");
            var currentDll = Path.Combine(pluginDirectory, IsHostMode ? HostDllName : ClientDllName);
            var targetDll = Path.Combine(pluginDirectory, useHostMode ? HostDllName : ClientDllName);
            var sourceDll = useHostMode
                ? Path.Combine(bepinExRoot, "plugins_host", HostDllName)
                : Path.Combine(bepinExRoot, "plugins_nonhost", ClientDllName);
            var switchLogPath = Path.Combine(bepinExRoot, "tohhamo_mode_switch.log");

            if (!File.Exists(sourceDll))
            {
                ShowWarning(useHostMode
                    ? "ホスト専用MODのDLLが見つかりません。Build-TOHhamo-Modes.cmdで両方をビルドしてください。"
                    : "参加者専用MODのDLLが見つかりません。Build-TOHhamo-Modes.cmdで両方をビルドしてください。");
                return;
            }

            Directory.CreateDirectory(pluginDirectory);
            AppendSwitchLog(switchLogPath, $"Pko-style mode switch requested. Source={sourceDll}; Current={currentDll}; Target={targetDll}");

            // Pko版のBackupDLLと同様に、現在ロード中のDLLを.dll.bakへ退避する。
            // BepInExは拡張子.dll以外をプラグインとしてロードしないため、次回起動時に二重ロードしない。
            var currentBackup = currentDll + ".bak";
            if (File.Exists(currentBackup)) File.Delete(currentBackup);
            if (File.Exists(currentDll)) File.Move(currentDll, currentBackup);

            // 以前の切替で残存し得る反対モードDLLは、次回の二重ロードを防ぐため削除する。
            var oppositeDll = Path.Combine(pluginDirectory, useHostMode ? ClientDllName : HostDllName);
            if (!string.Equals(oppositeDll, currentDll, StringComparison.OrdinalIgnoreCase) && File.Exists(oppositeDll))
                File.Delete(oppositeDll);

            // Pko版のDownloadDLLと同じく、選択DLLを最終配置先へ直接書き込む。
            File.Copy(sourceDll, targetDll, true);
            var sourceLength = new FileInfo(sourceDll).Length;
            var targetLength = new FileInfo(targetDll).Length;
            if (sourceLength != targetLength)
                throw new IOException($"DLL size verification failed: source={sourceLength}, target={targetLength}");

            AppendSwitchLog(switchLogPath, $"Pko-style DLL activation completed. Target={targetDll}; Size={targetLength}");
            isSwitching = true;
            ShowWarning(useHostMode
                ? "MODを入れ替えました。\nホスト専用MODを準備しました。\nAmong Usを起動してください。"
                : "MODを入れ替えました。\n参加者専用MODを準備しました。\nAmong Usを起動してください。");

            // Pko版のバージョン変更と同様、DLL配置完了後はゲームを終了する。
            // 自動でAmong Us.exeを直起動しないため、BepInEx未読込の通常版起動を防止する。
            _ = new LateTask(() => Application.Quit(), 1.5f, "ModeSwitchQuitAfterPkoStyleActivation", true);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "ModeSwitchPkoStyle");
            ShowWarning("MOD切替に失敗しました。切替前のDLLは.bakとして保持されています。詳細はtohhamo_mode_switch.logを確認してください。");
        }
    }

    private static void AppendSwitchLog(string path, string message)
    {
        try
        {
            File.AppendAllText(path, $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ログ保存失敗はDLL切替の成否を妨げない。
        }
    }
}
