using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

public static class Program
{
    private static TrayApplication trayApp;
    private static MainWindowController controller;
    private static string exePath;
    private static System.Threading.Mutex appMutex;
    private static System.Threading.EventWaitHandle startupEvent;
    private static System.Threading.Timer launchLifetime;
    private static int startupSteamPid;

    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;


        // Parse command line arguments
        if (args.Length > 0)
        {
            if (args[0] == "--steam-bootstrap")
            {
                try { SteamBootstrap.Start(exePath); }
                catch (Exception ex) {
                    string directory = Path.GetDirectoryName(AppSettings.ConfigPath); Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "startup-error.log"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args[0].Equals("--launch", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                bool steamWasRunning = SteamSession.IsRunning();
                launchLifetime = new System.Threading.Timer(_ => { if (SteamSession.ShouldExit(steamWasRunning, SteamSession.IsRunning(), false, false)) Environment.Exit(0); }, null, 1000, 1000);
                // Run in headless game launcher mode
                string gameName = args[1];
                GameLauncher.LaunchAndMonitor(gameName);
                return;
            }
            else if (args[0].Equals("--sync-silent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var api = new EpicApiManager();
                    int added, removed;
                    string log = SteamSession.Synchronize(exePath, api, out added, out removed);
                    string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamEpicSync");
                    Directory.CreateDirectory(logDir);
                    File.WriteAllText(Path.Combine(logDir, "last-sync.log"), log + "\n" + api.LastError);
                }
                catch (Exception ex) { Environment.ExitCode = 1; Console.Error.WriteLine(ex.Message); }
                return;
            }
            else if (args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show(
                    "Вы действительно хотите удалить все импортированные ярлыки из Steam, убрать утилиту из автозагрузки Windows и полностью стереть все сохраненные настройки и кэши?",
                    "Удаление Steam Epic Sync",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // 1. Disable startup
                        SetRunOnStartup(false);

                        // 2. Delete EGS closing settings from registry
                        try
                        {
                            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\SteamEpicSync", false);
                        }
                        catch {}

                        // 3. Clear Steam shortcuts (pass empty list to delete all ours)
                        int added, removed;
                        EpicSyncManager.Sync(exePath, new System.Collections.Generic.List<EpicGame>(), out added, out removed, true);

                        // 4. Delete local AppData folder
                        string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamEpicSync");
                        if (Directory.Exists(localAppDir))
                        {
                            Directory.Delete(localAppDir, true);
                        }

                        MessageBox.Show(
                            string.Format("Все настройки, кэши и ярлыки Steam ({0} шт.) были успешно удалены.\n\nТеперь вы можете просто удалить файл SteamEpicSync.exe.", removed),
                            "Удаление завершено",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Ошибка при удалении файлов: " + ex.Message,
                            "Ошибка",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
                return;
            }
        }

        bool steamSession = args.Length > 0 && args[0] == "--steam-session";
        // GUI/Tray mode: Check for duplicate instance
        bool createdNew;
        appMutex = new System.Threading.Mutex(true, "SteamEpicSync_SingleInstanceMutex_TrayGUI", out createdNew);
        if (!createdNew)
        {
            if (steamSession)
            {
                try { using (var signal = System.Threading.EventWaitHandle.OpenExisting("Local\\SteamEpicSync_Startup")) signal.Set(); } catch { }
                return;
            }
            MessageBox.Show("Приложение Steam Epic Sync уже запущено и находится в системном трее (в правом нижнем углу экрана рядом с часами).",
                "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var settings = AppSettings.Load();
        settings.ExePath = exePath;
        settings.Save();
        startupEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "Local\\SteamEpicSync_Startup");
        var listener = new System.Threading.Thread(() => { while (true) { startupEvent.WaitOne(); RequestStartupSync(); } });
        listener.IsBackground = true;
        listener.Start();

        // Default: Start System Tray Application
        trayApp = new TrayApplication(
            exePath,
            TriggerSyncNow,
            ShowDashboard,
            IsRunOnStartup,
            SetRunOnStartup,
            steamSession
        );

        // Run application message loop
        if (steamSession) RequestStartupSync();
        else ShowDashboard();
        Application.Run(trayApp);
    }

    private static void RequestStartupSync()
    {
        int steamPid = SteamSession.CurrentSteamPid();
        if (steamPid != 0 && System.Threading.Interlocked.Exchange(ref startupSteamPid, steamPid) == steamPid) return;
        var settings = AppSettings.Load();
        if (SteamSession.ConsumeStartupSkip() || !settings.SyncOnSteamStart)
        {
            if (!settings.LaunchWithSteam) Environment.Exit(0);
            return;
        }
        System.Threading.ThreadPool.QueueUserWorkItem(_ => {
            try {
                int added, removed;
                SteamSession.Synchronize(exePath, new EpicApiManager(), out added, out removed);
                ShowTrayBalloon("Синхронизация при запуске", "Библиотека обновлена. Steam запущен снова.");
            } catch (Exception ex) { ShowTrayBalloon("Ошибка синхронизации", ex.Message, ToolTipIcon.Error); }
            finally { if (!AppSettings.Load().LaunchWithSteam) Environment.Exit(0); }
        });
    }

    public static void ShowDashboard()
    {
        if (controller == null)
        {
            try
            {
                controller = new MainWindowController(exePath);
                // When closing the window, cancel the close and hide it instead
                controller.Window.Closing += (s, e) =>
                {
                    e.Cancel = true;
                    controller.Window.Hide();
                };
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка запуска панели: " + ex.Message, "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
        }

        controller.RefreshSettings();
        controller.Window.Show();
        controller.Window.Activate();
        controller.RefreshData();
    }

    public static void TriggerSyncNow()
    {
        if (controller != null && controller.Window.IsVisible)
        {
            controller.TriggerSync();
        }
        else
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    int added, removed;
                    SteamSession.Synchronize(exePath, new EpicApiManager(), out added, out removed);
                    ShowTrayBalloon("Синхронизация успешна",
                        string.Format("Добавлено игр: {0}. Библиотека обновлена.", added),
                        ToolTipIcon.Info);
                }
                catch (Exception ex)
                {
                    ShowTrayBalloon("Ошибка синхронизации", ex.Message, ToolTipIcon.Error);
                }
            });
        }
    }

    public static void ShowTrayBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (trayApp != null) trayApp.ShowNotification(title, text, icon);
    }

    public static void UpdateTrayMenu()
    {
        if (trayApp != null) trayApp.UpdateStartupMenuItem();
    }

    // --- Settings / Registry Helpers ---

    public static bool IsRunOnStartup()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key != null)
                {
                    var val = key.GetValue("SteamEpicSync") as string;
                    return !string.IsNullOrEmpty(val);
                }
            }
        }
        catch { }
        return false;
    }

    public static void SetRunOnStartup(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    if (enable)
                    {
                        key.SetValue("SteamEpicSync", "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue("SteamEpicSync", false);
                    }
                }
            }
        }
        catch { }
    }

    public static bool GetCloseEgsSetting()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\SteamEpicSync"))
            {
                if (key != null)
                {
                    var val = key.GetValue("CloseEgs");
                    if (val != null)
                        return (int)val != 0;
                }
            }
        }
        catch { }
        return true; // Default to true (smart EGS closure enabled)
    }

    public static void SetCloseEgsSetting(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\SteamEpicSync"))
            {
                if (key != null)
                {
                    key.SetValue("CloseEgs", enable ? 1 : 0);
                }
            }
        }
        catch { }
    }

}
