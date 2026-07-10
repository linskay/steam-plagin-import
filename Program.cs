using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

public static class Program
{
    private static TrayApplication trayApp;
    private static MainWindowController controller;
    private static string exePath;

    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

        // Parse command line arguments
        if (args.Length > 0)
        {
            if (args[0].Equals("--launch", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                // Run in headless game launcher mode
                string gameName = args[1];
                GameLauncher.LaunchAndMonitor(gameName);
                return;
            }
            else if (args[0].Equals("--sync-silent", StringComparison.OrdinalIgnoreCase))
            {
                // Run in headless silent sync mode
                var games = EpicSyncManager.FindInstalledGames();
                int added, removed;
                string log = EpicSyncManager.Sync(exePath, games, out added, out removed);
                Console.WriteLine(log);
                Console.WriteLine(string.Format("Sync complete. Added: {0}, Removed: {1}", added, removed));
                return;
            }
        }

        // Default: Start System Tray Application
        trayApp = new TrayApplication(
            exePath,
            TriggerSyncNow,
            ShowDashboard,
            IsRunOnStartup,
            SetRunOnStartup
        );

        // Run initial silent sync in background
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var games = EpicSyncManager.FindInstalledGames();
                int added, removed;
                EpicSyncManager.Sync(exePath, games, out added, out removed);
                if (added > 0 || removed > 0)
                {
                    ShowTrayBalloon("Авто-синхронизация", 
                        string.Format("Библиотека синхронизирована. Добавлено: {0}, Удалено: {1}.\nПожалуйста, перезапустите Steam.", added, removed),
                        ToolTipIcon.Info);
                }
            }
            catch { }
        });

        // Run application message loop
        Application.Run(trayApp);
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
                    var games = EpicSyncManager.FindInstalledGames();
                    int added, removed;
                    EpicSyncManager.Sync(exePath, games, out added, out removed);
                    ShowTrayBalloon("Синхронизация успешна", 
                        string.Format("Синхронизировано игр: {0}.\nПожалуйста, перезапустите Steam, если он запущен.", games.Count),
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
