using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

public static class SteamSession
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);
    private static bool MainWindowVisible()
    {
        bool visible = false;
        foreach (var process in Process.GetProcessesByName("steamwebhelper"))
            using (process)
                if (process.MainWindowTitle == "Steam" && process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle) && !IsIconic(process.MainWindowHandle)) visible = true;
        return visible;
    }
    public static volatile bool Restarting;
    private static int synchronizing;
    private static DateTime restartGrace = DateTime.MinValue;
    public static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("steam");
        bool running = processes.Length > 0;
        foreach (var process in processes) process.Dispose();
        return running;
    }
    public static int CurrentSteamPid()
    {
        var processes = Process.GetProcessesByName("steam");
        int id = processes.Length > 0 ? processes[0].Id : 0;
        foreach (var process in processes) process.Dispose();
        return id;
    }
    public static bool ShouldExit(bool hasSeenSteam, bool running, bool restarting, bool inGrace)
    {
        return hasSeenSteam && !running && !restarting && !inGrace;
    }
    public static bool InRestartGrace { get { return DateTime.UtcNow < restartGrace; } }
    public static void AcknowledgeSteamStarted() { if (!Restarting) restartGrace = DateTime.MinValue; }
    private static string SkipPath { get { return Path.Combine(Path.GetDirectoryName(AppSettings.ConfigPath), "startup-skip.txt"); } }
    public static bool ConsumeStartupSkip()
    {
        if (!File.Exists(SkipPath)) return false;
        string raw = File.ReadAllText(SkipPath);
        File.Delete(SkipPath);
        long expiry;
        return long.TryParse(raw, out expiry) && expiry > DateTime.UtcNow.Ticks;
    }
    public static string Synchronize(string exePath, EpicApiManager api, out int added, out int removed)
    {
        if (Interlocked.CompareExchange(ref synchronizing, 1, 0) != 0) throw new InvalidOperationException("Синхронизация уже выполняется.");
        added = removed = 0;
        bool restart = false;
        bool showSteam = false;
        try
        {
            var games = EpicSyncManager.GetLibrary(api, true);
            var processes = Process.GetProcessesByName("steam");
            if (processes.Length > 0)
            {
                Restarting = true;
                showSteam = MainWindowVisible();
                Directory.CreateDirectory(Path.GetDirectoryName(SkipPath));
                File.WriteAllText(SkipPath, DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
                using (var command = Process.Start(new ProcessStartInfo(Path.Combine(EpicSyncManager.GetSteamPath(), "steam.exe"), "-shutdown") { UseShellExecute = false, CreateNoWindow = true })) { }
                foreach (var process in processes)
                {
                    using (process) if (!process.WaitForExit(30000)) throw new InvalidOperationException("Steam не завершился. Закройте игру или диалог Steam и повторите синхронизацию.");
                }
                restart = true;
            }
            string log = EpicSyncManager.Sync(exePath, games, out added, out removed);
            string directory = Path.GetDirectoryName(AppSettings.ConfigPath);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "last-sync.log"), DateTime.Now.ToString("s") + "\n" + log + "\n" + api.LastError);
            return log;
        }
        finally
        {
            try
            {
                if (restart)
                {
                    restartGrace = DateTime.UtcNow.AddSeconds(30);
                    using (var process = Process.Start(new ProcessStartInfo(Path.Combine(EpicSyncManager.GetSteamPath(), "steam.exe"), showSteam ? "steam://open/games" : "-silent") { UseShellExecute = false, CreateNoWindow = true })) { }
                }
                else if (File.Exists(SkipPath)) File.Delete(SkipPath);
            }
            finally { Restarting = false; Interlocked.Exchange(ref synchronizing, 0); }
        }
    }
}
