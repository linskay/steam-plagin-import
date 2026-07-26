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

    [STAThread]
    public static void Main(string[] args)
    {
        // Force software rendering mode in WPF to prevent NVIDIA overlay from hooking it as a game
        try
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        SetBrowserEmulationKey();

        bool isAutorun = false;

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
                        EpicSyncManager.Sync(exePath, new System.Collections.Generic.List<EpicGame>(), out added, out removed);

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
            else if (args[0].Equals("--autorun", StringComparison.OrdinalIgnoreCase))
            {
                isAutorun = true;
            }
        }

        // GUI/Tray mode: Check for duplicate instance
        bool createdNew;
        appMutex = new System.Threading.Mutex(true, "SteamEpicSync_SingleInstanceMutex_TrayGUI", out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Приложение Steam Epic Sync уже запущено и находится в системном трее (в правом нижнем углу экрана рядом с часами).", 
                "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool firstLaunch = false;
        if (!isAutorun)
        {
            firstLaunch = IsFirstLaunch();
            if (firstLaunch)
            {
                SetFirstLaunchCompleted();
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

        if (firstLaunch)
        {
            ShowDashboard();
        }

        // Run initial silent sync in background
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var games = EpicSyncManager.FindInstalledGames();
                int added, removed;
                EpicSyncManager.Sync(exePath, games, out added, out removed);
                if ((added > 0 || removed > 0) && !firstLaunch)
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
                        key.SetValue("SteamEpicSync", "\"" + exePath + "\" --autorun");
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

    private static bool IsFirstLaunch()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\SteamEpicSync"))
            {
                if (key != null)
                {
                    var val = key.GetValue("FirstLaunchCompleted");
                    if (val != null)
                    {
                        return Convert.ToInt32(val) == 0;
                    }
                }
            }
        }
        catch { }
        return true;
    }

    private static void SetFirstLaunchCompleted()
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\SteamEpicSync"))
            {
                if (key != null)
                {
                    key.SetValue("FirstLaunchCompleted", 1, RegistryValueKind.DWord);
                }
            }
        }
        catch { }
    }

    private static void SetBrowserEmulationKey()
    {
        try
        {
            string fileName = Path.GetFileName(exePath);
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
            {
                if (key != null)
                {
                    key.SetValue(fileName, 11001, RegistryValueKind.DWord);
                }
            }
        }
        catch { }
    }
}
